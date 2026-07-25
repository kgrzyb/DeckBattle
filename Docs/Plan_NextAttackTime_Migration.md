# Plan migracji cooldownu ataku do `NextAttackTime`

## Status

Plan techniczny. Implementacja nie została jeszcze rozpoczęta.

## Cel

Zastąpić model dekrementowanego `UnitRuntimeState.AttackCooldownRemaining`
modelem opartym o absolutny czas symulacji:

- `BattleSimulation.ElapsedTime`;
- `UnitRuntimeState.NextAttackTime`.

Migracja ma:

- zachować deterministyczny przebieg walki;
- zachować istniejącą semantykę overshootu cooldownu;
- nie zwiększyć kosztu ticka na mobile;
- nie rozszerzać zakresu na niepotrzebny refactor pozostałych timerów;
- jasno zdefiniować zachowanie ruchu, CC, mnożników, różnych speciali i
  pocisków;
- zostać zabezpieczona testami charakterystyki i regresji.

## Stan obecny

- `BattleTickLoop` steruje walką stałym tickiem.
- `CombatResolver` odejmuje `tickDuration` od
  `AttackCooldownRemaining`, a następnie sprawdza gotowość ataku.
- Po udanym ataku efektywny cooldown jest dodawany do aktualnej reszty:

  ```csharp
  attacker.AttackCooldownRemaining += effectiveCooldown;
  ```

  Dzięki temu ujemna reszta zachowuje overshoot ticka.
- Cooldown płynie podczas ruchu. `IsMoving` blokuje jedynie wykonanie ataku.
- Obecny special jest zakodowany bezpośrednio w `CombatResolver`: ustawia
  runtime `AttackCooldownMultiplier` na `0.5f` przez `5f`.
- Docelowo różne jednostki mogą posiadać różne speciale. Nie każdy special
  musi modyfikować cooldown ani być efektem czasowym.
- Ranged rozpoczyna cooldown przy wystrzale. Obrażenia pocisku są
  rozstrzygane później.
- Attack intents są zbierane i rozstrzygane w kolejności
  `BattleSimulation.Units`.
- W warstwie `BattleSimulation` nie ma obecnie systemu CC ani dynamicznych
  summonów lub respawnów.

## Docelowy model

### Autorytatywny zegar

W `BattleSimulation` należy dodać:

```csharp
public double ElapsedTime { get; private set; }
```

`ElapsedTime` zaczyna się od `0.0`. `BattleTickLoop` zwiększa go dokładnie
raz dla każdego aktywnego ticka:

```csharp
simulation.AdvanceTime(TickDuration);
```

Przesunięcie czasu powinno nastąpić:

1. po sprawdzeniu `simulation.IsBattleEnded`;
2. przed aktualizacją ruchu, pocisków, efektów czasowych i walki.

Pierwszy tick `0.35` rozstrzyga więc stan dla czasu `0.35`. Odpowiada to
obecnej kolejności, w której cooldown jest najpierw zmniejszany, a następnie
sprawdzany.

`AdvanceTime` powinno być `internal`. Logika walki nie może korzystać z
`Time.time`, `Time.deltaTime` ani czasu animacji.

Typ `double` jest rekomendowany dla `ElapsedTime` i terminów absolutnych.
Koszt pamięci i obliczeń jest pomijalny przy skali tej symulacji, a precyzja
jest bezpieczniejsza w dłuższych walkach niż przy akumulowanym `float`.

### Termin następnego ataku

W `UnitRuntimeState` należy zastąpić:

```csharp
public float AttackCooldownRemaining;
```

przez:

```csharp
public double NextAttackTime;
```

Gotowość ataku:

```csharp
simulation.ElapsedTime >= unit.NextAttackTime
```

Nie należy utrzymywać obu pól jako równoległych, zapisywalnych źródeł prawdy.
Jeżeli UI lub debugowanie potrzebuje pozostałego czasu, należy obliczyć go na
żądanie:

```csharp
Math.Max(0.0, unit.NextAttackTime - simulation.ElapsedTime)
```

### Pierwszy atak

Przy rejestrowaniu jednostki w symulacji:

```csharp
unit.NextAttackTime =
    simulation.ElapsedTime +
    simulation.Tuning.GetAttackCooldown(unit.Definition, unit);
```

Na początku walki `ElapsedTime == 0`, więc jednostka czeka pełny efektywny
cooldown. Globalny mnożnik z `BattleRuntimeTuning` i runtime multiplier
jednostki są uwzględniane od pierwszego ataku.

Planowanie pierwszego ataku powinno należeć do `BattleSimulation`, ponieważ
sam `UnitRuntimeState` nie zna aktualnego czasu ani globalnego tuningu.
Niepodłączony runtime state może mieć `NextAttackTime =
double.PositiveInfinity`, aby nie stał się przypadkowo natychmiast gotowy.

## Jawne decyzje dotyczące zachowania

### Overshoot po udanym ataku

Po faktycznie wykonanym ataku należy użyć:

```csharp
attacker.NextAttackTime +=
    simulation.Tuning.GetAttackCooldown(attacker.Definition, attacker);
```

Nie używać:

```csharp
attacker.NextAttackTime =
    simulation.ElapsedTime + effectiveCooldown;
```

Jest to jawna decyzja o kontynuacji od poprzednio zaplanowanego terminu.
Zachowuje istniejący overshoot i nie przesuwa częstotliwości ataków przez
kwantyzację ticków.

Jednostka nadal może wykonać najwyżej jeden atak na tick. Jeśli po jednym
ataku jej nowy termin nadal znajduje się w przeszłości, może wykonać kolejny
atak dopiero w następnym ticku. Nie należy dodawać pętli nadrabiającej wiele
ataków.

Harmonogram przesuwa się tylko po rzeczywiście wykonanym ataku. Jeżeli intent
zostanie pominięty, ponieważ wcześniejszy killing blow zabił cel, termin nie
ulega zmianie.

### Globalny i runtime `AttackCooldownMultiplier`

Efektywny cooldown pozostaje liczony przez istniejące
`BattleRuntimeTuning.GetAttackCooldown`:

```text
definition.AttackCooldown
* global AttackCooldownMultiplier
* runtime AttackCooldownMultiplier
```

z zachowaniem minimalnej wartości `0.01f`.

Runtime multiplier powinien być wartością wynikową stanu jednostki, a nie
niejawnym dowodem, że aktywny jest konkretny special. Special przyspieszający
atak może wnosić mnożnik `0.5f`, natomiast special leczący, zadający obrażenia
lub modyfikujący inną statystykę pozostawia mnożnik cooldownu równy `1f`.

Mnożniki są próbkowane:

- podczas planowania pierwszego ataku;
- po każdym udanym ataku, przy planowaniu następnego terminu.

### Zmiana mnożnika w trakcie cooldownu

Decyzja balansowa: zmiana mnożnika nie przelicza już zaplanowanego
`NextAttackTime`.

Aktywacja lub wygaśnięcie mnożnika wpływa dopiero na cooldown dodawany po
następnym udanym ataku. Odpowiada to obecnemu zachowaniu
`AttackCooldownRemaining`, ogranicza stan i nie dokłada pracy do ticka.

Należy zachować obecną kolejność efektów: jeśli special przyspieszający atak
aktywuje się przez manę uzyskaną z właśnie wykonanego ataku, jego mnożnik
obowiązuje już przy planowaniu następnego ataku.

### Różne speciale

Plan nie powinien utrwalać założenia, że każdy special oznacza
`AttackCooldownMultiplier = 0.5f` przez `5f`. Minimalny docelowy model
powinien rozdzielać:

- definicję speciala, czyli niezmienne dane;
- runtime aktywnego speciala lub efektu;
- wykonanie efektu;
- harmonogram zwykłych ataków.

Rekomendowany mały zakres to jedna definicja speciala na jednostkę i najwyżej
jeden aktywny runtime tego speciala na jednostkę. Pozwala to mieć różne
speciale między jednostkami bez wprowadzania ogólnego systemu buffów i
alokowanych list efektów.

Przykładowe dane definicji:

```csharp
public enum UnitSpecialKind
{
    None,
    AttackSpeed,
    // Kolejne rodzaje są dodawane jawnie wraz z ich resolverem.
}

public sealed class UnitSpecialDefinition : ScriptableObject
{
    public string SpecialId;
    public UnitSpecialKind Kind;
    public float Duration;
    public float AttackCooldownMultiplier = 1f;
}
```

`UnitDefinition` powinno wskazywać `UnitSpecialDefinition`. Nie należy w tej
migracji projektować uniwersalnego grafu efektów, refleksji ani hierarchii
klas dziedziczonych dla każdego speciala. Prosty enum i jawny resolver są
łatwiejsze do profilowania, serializacji i testowania w Unity.

Minimalny runtime może przechowywać:

```csharp
public UnitSpecialDefinition ActiveSpecial;
public double SpecialEndTime;
public float SpecialAttackCooldownMultiplier;
```

Aktywność wynika z `ActiveSpecial != null`; nie jest potrzebna druga flaga,
która mogłaby utracić synchronizację. Special natychmiastowy wykonuje efekt,
emituje event i nie pozostaje jako `ActiveSpecial`.

`SpecialAttackCooldownMultiplier` zaczyna od `1f` i stanowi wyłącznie wkład
aktywnego speciala do efektywnego cooldownu. Dzięki temu wygaśnięcie speciala
nie powinno wykonywać ogólnego:

```csharp
unit.AttackCooldownMultiplier = 1f;
```

które mogłoby przypadkowo skasować mnożnik pochodzący z innego przyszłego
systemu. `BattleRuntimeTuning.GetAttackCooldown` powinno korzystać z jawnie
skomponowanej wartości runtime, początkowo obejmującej globalny multiplier i
`SpecialAttackCooldownMultiplier`.

Speciale należy podzielić na dwie kategorie czasowe:

- natychmiastowe: efekt jest wykonywany przy aktywacji i nie tworzy terminu
  wygaśnięcia;
- czasowe: aktywacja ustawia `SpecialEndTime = ElapsedTime + Duration`, a
  resolver usuwa wyłącznie wkład tego speciala po osiągnięciu terminu.

Obecny special staje się pierwszą definicją:

```text
Kind: AttackSpeed
Duration: 5
AttackCooldownMultiplier: 0.5
```

Ponowna aktywacja tego samego czasowego speciala odświeża termin do pełnego
`Duration` od bieżącego `ElapsedTime`. W wersji objętej tym planem speciale
nie stackują się. Jeżeli w przyszłości jedna jednostka ma utrzymywać kilka
równoległych efektów, należy dodać osobny, ograniczony pojemnością kontener
runtime lub pulę; nie należy dodawać alokowanej listy przetwarzanej LINQ w
każdym ticku.

`UnitSpecialActivated` powinien identyfikować special przez stabilny
`UnitSpecialKind` lub identyfikator definicji oraz nadal przekazywać
deklarowaną długość efektu. Prezentacja będzie wtedy mogła dobrać właściwą
animację i VFX bez rozpoznawania speciala po samej wartości `duration`.

`CombatResolver.AddMana` powinno odpowiadać tylko za próg i reset many, a
następnie delegować aktywację do małego `UnitSpecialResolver`. Resolver
speciali otrzymuje `BattleSimulation`, jednostkę, definicję oraz kolejkę
eventów i jawnie obsługuje `UnitSpecialKind`. Dzięki temu dodanie kolejnego
speciala nie rozbudowuje kodu harmonogramu zwykłych ataków.

W ramach migracji nie trzeba implementować drugiego pełnego gameplayowego
speciala. Należy jednak usunąć założenia uniemożliwiające jego dodanie:
hardcodowane wartości, pole czasu nazwane wyłącznie dla attack speed oraz
event bez identyfikatora speciala. Testy powinny obejmować obecny
`AttackSpeed` i ścieżkę jednostki bez speciala; pierwszy nowy rodzaj speciala
musi później otrzymać własne testy aktywacji, czasu i interakcji z atakiem.

W celu zachowania obecnego balansu należy utworzyć współdzieloną definicję
attack-speed speciala z wartościami `0.5f` i `5f` oraz przypisać ją wszystkim
istniejącym definicjom jednostek, które obecnie korzystają z hardcodowanego
zachowania. Brak przypisania nie może po cichu oznaczać haste; jednostka bez
definicji speciala po osiągnięciu progu many nie aktywuje efektu i powinna
zostać objęta jawnym testem.

Migracja nie obejmuje timerów ruchu ani lotu pocisków. Są to niezależne,
krokowe procesy, a ich zmiana nie jest potrzebna do wdrożenia nowego
schedulera ataku.

### Ruch

Należy zachować istniejącą semantykę:

- `NextAttackTime` płynie podczas ruchu;
- `IsMoving` blokuje wykonanie ataku, ale nie zatrzymuje zegara;
- po zakończeniu ruchu jednostka może zaatakować w tym samym ticku, jeżeli
  jej termin już minął;
- jednostka nie nadrabia wielu ataków naraz.

### CC, stun, silence i disable

System CC nie jest obecnie zaimplementowany. Dla przyszłego systemu zostaje
ustalony kontrakt:

- stun i attack-disable blokują wykonanie ataku, ale `NextAttackTime` płynie
  w tle;
- po zdjęciu blokady jednostka atakuje przy pierwszej legalnej okazji, jeżeli
  termin minął;
- silence domyślnie nie blokuje basic attack, tylko special lub ability,
  chyba że definicja statusu jawnie stanowi inaczej;
- po długim CC nie następuje wielokrotny burst w jednym ticku.

Zamrażanie zegara ataku wymagałoby dodatkowego stanu pauzy i przesuwania
terminu. Może zostać dodane później jako osobna, świadoma mechanika
balansowa, ale nie jest częścią tej migracji.

### Ranged projectile

Cooldown rozpoczyna się przy wystrzale:

- `NextAttackTime` zostaje przesunięty po utworzeniu
  `ProjectileRuntimeState`;
- późniejszy impact nie modyfikuje harmonogramu atakującego;
- śmierć celu przed impactem nie zwraca cooldownu;
- brak definicji pocisku nadal korzysta z obecnego fallbacku natychmiastowych
  obrażeń.

### Deterministyczna kolejność ataków

Podczas migracji należy zachować obecną kolejność:

1. attack intents są zbierane w kolejności `BattleSimulation.Units`;
2. są wykonywane sekwencyjnie w tej samej kolejności;
3. intent skierowany w cel zabity przez wcześniejszy intent jest pomijany;
4. wzajemne ataki przygotowane przed rozstrzygnięciem mogą oba dojść do
   skutku zgodnie z obecną fazą zbierania intentów.

`BattleSimulation.Units` staje się formalnie kanoniczną kolejnością
symulacji. Nie należy dodawać sortowania w każdym ticku, ponieważ zmieniłoby
to wyniki istniejących walk i zwiększyło koszt.

Jeżeli w przyszłości wynik ma być niezależny od kolejności `spawnData`,
przejście na kolejność `UnitId` powinno być osobną zmianą z własnymi testami
balansowymi.

### Summony i respawny

Przyszła metoda dynamicznego spawnu musi planować atak względem aktualnego
czasu:

```csharp
unit.NextAttackTime =
    simulation.ElapsedTime +
    simulation.Tuning.GetAttackCooldown(unit.Definition, unit);
```

Nie wolno ustawiać samego cooldownu względem zera. Respawn istniejącego
runtime state również musi ponownie zaplanować pierwszy atak względem
bieżącego `ElapsedTime`.

Obecny `BattleTickLoop` tworzy workspace'y i tablice targetowania według
początkowej liczby jednostek. Przed dodaniem summonów trzeba zapewnić ich
kontrolowane powiększanie lub odtworzenie. Nie jest to część tej migracji.

## Kolejność wdrożenia

### Krok 1: testy charakterystyki

Przed zmianą produkcyjną utrwalić:

- pełny cooldown przed pierwszym atakiem;
- overshoot dla cooldownów `0.5` i `0.7` przy ticku `0.35`;
- cooldown płynący podczas ruchu;
- aktywację, odświeżenie i wygaśnięcie obecnego speciala attack speed;
- brak wpływu jednostki bez przypisanej definicji speciala na harmonogram
  `NextAttackTime`;
- kolejność killing blow;
- oddzielenie launchu pocisku od impactu.

Testy charakterystyki są bramką wejściową. Implementacja nie powinna się
rozpocząć, dopóki te testy nie przechodzą na obecnym modelu.

### Krok 2: zegar bez zmiany cooldownu

Zmiany:

- `BattleSimulation.cs`: `ElapsedTime` i `AdvanceTime`;
- `BattleTickLoop.cs`: jedno przesunięcie zegara na aktywny tick;
- `BattleTickLoopTests.cs`: przyrost czasu i brak przyrostu po zakończeniu
  walki.

Cooldown nadal działa po staremu. Ten krok ma być neutralny funkcjonalnie.

### Krok 3: atomowe przełączenie cooldownu

Zmiany:

- `UnitRuntimeState.cs`: `NextAttackTime` i reset stanu;
- `BattleSimulation.cs`: inicjalizacja harmonogramu;
- `CombatResolver.cs`: porównanie absolutne i reset przez
  `old NextAttackTime + cooldown`;
- usunięcie `ReduceCooldown`;
- usunięcie parametru `tickDuration` z `CombatResolver`;
- aktualizacja wszystkich testów odnoszących się do
  `AttackCooldownRemaining`.

Po tym kroku `AttackCooldownRemaining` nie powinno pozostawać w kodzie.
Testy upływu czasu powinny używać `BattleTickLoop`. Bezpośrednie testy
`CombatResolver` mogą ustawiać `NextAttackTime` jako gotowy względem
bieżącego czasu, ale nie powinny przesuwać zegara przez resolver.

### Krok 4: definicje i absolutny czas różnych speciali

Zmiany:

- dodanie lekkiej, data-driven definicji speciala i przypisania jej do
  `UnitDefinition`;
- utworzenie współdzielonego assetu obecnego attack-speed speciala i
  przypisanie go istniejącym jednostkom z zachowaniem plików `.meta`;
- przeniesienie hardcodowanego `0.5f` / `5f` z `CombatResolver` do definicji
  obecnego speciala;
- zastąpienie `SpecialDurationRemaining` przez ogólny `SpecialEndTime` oraz
  jawny runtime wkładu speciala do modyfikowanych statystyk;
- przygotowanie kontraktu, w którym przyszły special natychmiastowy nie
  pozostawia sztucznego timera;
- wygaśnięcie czasowych speciali na podstawie `ElapsedTime`;
- identyfikowanie rodzaju speciala w evencie aktywacji;
- testy granicy czasu, ponownej aktywacji, ścieżki bez speciala i interakcji
  attack-speed speciala z resetem cooldownu.

### Krok 5: pełna regresja

Najpierw uruchomić wąskie Edit Mode tests:

- `BattleSimulationTests`;
- `CombatResolverTests`;
- `BattleRuntimeTuningTests`;
- `BattleTickLoopTests`;
- `ProjectileResolverTests`;
- `SpellPlayServiceTests`;
- `BattleSimulationCombatServiceTests`.

Następnie uruchomić pełne Edit Mode tests przez Unity Editor i Unity MCP.
Nie uruchamiać Edit Mode tests w Unity batchmode.

Pełna regresja i przegląd diffu są bramką wyjściową zadania.

## Oczekiwane wyniki dla ticka `0.35`

| Cooldown | Nominalne czasy ataku | Tiki z atakiem |
|---|---|---|
| `0.5` | `0.5`, `1.0`, `1.5`, `2.0` | `2`, `3`, `5`, `6` |
| `0.7` | `0.7`, `1.4`, `2.1` | `2`, `4`, `6` |

Przykład zachowania overshootu:

- drugi tick kończy się w czasie `0.70`;
- jednostka z cooldownem `0.5` miała termin `0.50`;
- po ataku nowy termin wynosi `1.00`;
- nie może wynieść `1.20`, ponieważ oznaczałoby to utratę overshootu.

## Testy do dodania lub zmiany

### Zegar

- `ElapsedTime` zmienia się `0 -> 0.35 -> 0.70 -> 1.05`;
- jest zwiększany dokładnie raz na tick;
- nie zwiększa się po zakończeniu walki;
- dwie symulacje z tym samym seedem i wejściem mają identyczny czas, stan i
  kolejność eventów.

### Inicjalizacja

- pierwszy termin uwzględnia bazowy cooldown;
- pierwszy termin uwzględnia globalny multiplier;
- runtime multiplier zaczyna od `1f`;
- reset lub przyszły spawn przy `ElapsedTime = 10` i cooldownie `1` daje
  `NextAttackTime = 11`, a nie `1`.

### Overshoot i częstotliwość

- regresja `0.5` / `0.7` przy ticku `0.35`;
- po ataku używany jest poprzedni termin, nie bieżący czas;
- duży tick powoduje najwyżej jeden atak jednostki;
- zaległy termin może umożliwić kolejny atak dopiero w kolejnym ticku.

### Mnożniki i różne speciale

- globalny multiplier wpływa na pierwszy i kolejne cooldowny;
- runtime multiplier wpływa na cooldown planowany po ataku;
- aktywacja mnożnika nie przesuwa już istniejącego terminu;
- attack-speed special aktywowany przez bieżący atak wpływa na następny
  cooldown;
- wygaśnięcie attack-speed speciala nie rozciąga już zaplanowanego terminu;
- czasowy special wygasa dokładnie przy swoim `SpecialEndTime`;
- ponowna aktywacja odświeża pełne `Duration`;
- definicja bez efektu czasowego nie pozostawia aktywnego timera;
- special niezwiązany z szybkością ataku nie zmienia `NextAttackTime` ani
  efektywnego cooldownu;
- wygaśnięcie jednego speciala usuwa tylko jego własny wkład do statystyk;
- event aktywacji przenosi stabilny identyfikator rodzaju speciala;
- jednostka bez przypisanej definicji nie otrzymuje domyślnego,
  hardcodowanego haste.

### Ruch i przyszłe CC

- poruszająca się gotowa jednostka nie atakuje;
- jej termin nie jest przesuwany przez ruch;
- po zakończeniu ruchu atakuje przy pierwszej legalnej okazji;
- testy CC należy dodać razem z systemem CC zgodnie z kontraktem opisanym w
  tym planie, bez dodawania fikcyjnych pól podczas obecnej migracji.

### Pociski

- wystrzał przesuwa `NextAttackTime`;
- impact nie przesuwa go ponownie;
- śmierć celu przed impactem nie przywraca cooldownu;
- pocisk może trafić po śmierci atakującego zgodnie z obecnym zachowaniem.

### Kolejność

- kilka gotowych jednostek zachowuje kolejność `BattleSimulation.Units`;
- wcześniejszy killing blow unieważnia późniejszy intent na ten sam cel;
- wzajemne, wcześniej zebrane intenty zachowują obecną semantykę;
- kolejność eventów jest identyczna w dwóch powtórzeniach z tym samym seedem.

Przy asercjach wartości `double` należy używać jawnej, małej tolerancji.
Produkcyjna gotowość pozostaje prostym porównaniem
`ElapsedTime >= NextAttackTime`.

## Ryzyka i moment ich weryfikacji

| Ryzyko | Kiedy sprawdzić | Weryfikacja |
|---|---|---|
| Atak o tick za wcześnie lub za późno | Krok 1 i 2 | Test granic pierwszego ataku oraz sekwencji czasu |
| Utrata overshootu | Krok 1 i 3 | Test `0.5` / `0.7` oraz asercja konkretnego następnego terminu |
| Wiele ataków jednostki w jednym ticku | Krok 3 | Test dużego overshootu |
| Niejawny rescheduling po zmianie mnożnika | Krok 3 i 4 | Test aktywacji oraz wygaśnięcia w trakcie oczekiwania |
| Hardcodowanie każdego speciala jako haste `0.5f` / `5f` | Krok 4 | Dane haste pochodzą z definicji, a ścieżka bez speciala nie aktywuje haste |
| Wygaśnięcie speciala zeruje cudzy modyfikator | Krok 4 | Test usuwania wyłącznie wkładu aktywnego speciala |
| Special natychmiastowy otrzymuje zbędny timer | Przy dodaniu pierwszego instant speciala | Obowiązkowy test efektu bez aktywnego `SpecialEndTime` |
| Istniejące jednostki tracą special po migracji danych | Krok 4 i 5 | Inspekcja assetów oraz test factory/symulacji dla istniejącej definicji |
| Zmiana kolejności killing blow | Krok 1, 3 i 5 | Test kolejności intentów, obrażeń, śmierci i eventów |
| Cooldown liczony od impactu | Krok 1, 3 i 5 | Test stanu bezpośrednio po launchu i po impact |
| Termin spawnu liczony od zera | Przy dodaniu API spawnu | Test spawnu przy niezerowym `ElapsedTime` |
| Dwa źródła prawdy cooldownu | Krok 3 i przegląd diffu | Brak `AttackCooldownRemaining` po przełączeniu |
| Resolver sam przesuwa czas | Krok 3 | API resolvera bez `tickDuration`, testy przez tick loop |
| Problemy precyzji czasu | Krok 2, 3 i pełna regresja | `double`, testy granic i dłuższej sekwencji ticków |
| Dynamiczne jednostki przekraczają workspace | Przed summonami | Osobna rozbudowa pojemności; poza obecnym zakresem |
| Regresja wydajności mobile | Krok 5 | Przegląd alokacji i Profiler na scenie walki |

## Pliki przewidziane do zmiany

Kod produkcyjny:

- `Assets/DeckBattle/Scripts/Battle/BattleSimulation.cs`;
- `Assets/DeckBattle/Scripts/Battle/UnitRuntimeState.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleTickLoop.cs`;
- `Assets/DeckBattle/Scripts/Battle/CombatResolver.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleEvent.cs`;
- lekki resolver speciali w `Assets/DeckBattle/Scripts/Battle/`;
- definicja speciala i enum rodzaju w `Assets/DeckBattle/Scripts/Data/`;
- `Assets/DeckBattle/Scripts/Data/UnitDefinition.cs`;
- asset obecnego attack-speed speciala i przypisania w
  `Assets/DeckBattle/Data/Units/`.

Prawdopodobnie bez zmiany zachowania, ale objęte weryfikacją:

- `Assets/DeckBattle/Scripts/Battle/BattleRuntimeTuning.cs`;
- `Assets/DeckBattle/Scripts/Battle/ProjectileResolver.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleSimulationCombatService.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleSimulationFactory.cs`.

Testy:

- `Assets/DeckBattle/Tests/EditMode/BattleSimulationTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/CombatResolverTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/BattleRuntimeTuningTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/BattleTickLoopTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/ProjectileResolverTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/SpellPlayServiceTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/BattleSimulationCombatServiceTests.cs`;
- testy definicji i resolvera speciali, w istniejącym pliku lub w osobnym
  `UnitSpecialResolverTests.cs`.

## Wpływ na mobile i rendering

- Zmiana nie dotyka URP, shaderów, tekstur, overdraw ani build size.
- Nie wymaga nowych pakietów.
- Nie dodaje alokacji na tick.
- Usunięcie dekrementowania cooldownu każdej żywej jednostki upraszcza hot
  path do odczytu czasu i jednego porównania.
- Obsługa różnych speciali powinna używać jednego jawnego switcha oraz
  stałego runtime state na jednostkę; bez refleksji, delegatów tworzonych przy
  aktywacji i list alokowanych w ticku.
- Nie należy dodawać sortowania, LINQ ani kolekcji tymczasowych w resolverze.
- Po wdrożeniu warto sprawdzić CPU time `BattleTickLoop.Tick`, liczbę
  alokacji GC na tick i stabilność czasu klatki w scenie z maksymalną
  przewidywaną liczbą jednostek.

## Kryteria zakończenia

Migrację można uznać za zakończoną, gdy:

- `AttackCooldownRemaining` nie występuje w kodzie runtime ani testach;
- `BattleSimulation.ElapsedTime` jest jedynym zegarem harmonogramu ataków;
- każdy aktywny tick przesuwa czas dokładnie raz;
- overshoot `0.5` / `0.7` przy ticku `0.35` jest zachowany;
- mnożniki oraz różne speciale zachowują opisany kontrakt;
- obecny haste `0.5f` / `5f` pochodzi z definicji, a nie z wartości
  hardcodowanych w `CombatResolver`;
- jednostka bez speciala nie otrzymuje haste ani zmiany harmonogramu ataków;
- event aktywacji jednoznacznie identyfikuje rodzaj speciala;
- ruch i pociski zachowują dotychczasowe działanie;
- kolejność rozstrzygania ataków pozostaje deterministyczna;
- wąskie i pełne Edit Mode tests przechodzą w Unity Editor;
- przegląd nie wykazuje nowych alokacji ani dodatkowego kosztu hot path;
- pozostałe ryzyka są opisane wraz z ewentualnym planem dalszej pracy.
