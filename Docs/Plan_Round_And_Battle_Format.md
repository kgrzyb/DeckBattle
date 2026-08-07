# Plan zmiany formatu rund i limitu jednostek

Status: propozycja implementacyjna
Data: 2026-08-06
Zakres: AP, dobieranie kart, limit jednostek, AI, HUD, konfiguracja i testy rund

## 1. Cel

Zmiana ma uprościć ekonomię rundy i zastąpić rosnące sloty wystawienia jednym stałym limitem jednostek na stronę.

Docelowe zasady:

| Reguła | Stan docelowy |
|---|---|
| AP w rundzie 1 | 1 AP |
| Przyrost AP | +1 AP w każdej następnej rundzie |
| Przenoszenie AP | brak; niewykorzystane AP przepada |
| Ręka początkowa | 3 karty |
| Dobór po rundzie | 1 karta na początku następnej rundy |
| Maksymalna ręka | 5 kart |
| Limit jednostek | 8 jednostek na stronę przez cały mecz |
| Sloty wystawienia | usunięte z modelu, konfiguracji i UI |

Przykład puli AP:

| Runda | AP na początku | AP przeniesione z poprzedniej rundy |
|---|---:|---:|
| 1 | 1 | 0 |
| 2 | 2 | 0 |
| 3 | 3 | 0 |
| 4 | 4 | 0 |

## 2. Ustalenia i granice zakresu

1. Reguły AP, ręki i limitu jednostek są symetryczne dla gracza i AI. Obie strony korzystają obecnie z tego samego `PlayerBattleState`, więc rozdzielenie tych wartości nie jest potrzebne.
2. Przy pełnej ręce dobór jest pomijany. Karta pozostaje w talii i może zostać dobrana w późniejszej rundzie po zwolnieniu miejsca. Jest to zgodne z aktualnym kontraktem `DeckService.DrawCards`.
3. Limit 10 dotyczy wszystkich jednostek zapisanych w trwałej formacji strony (`PlayerBattleState.Units`), a nie tylko jednostek żywych na końcu walki. Jednostki są obecnie odnawiane między rundami i pozostają częścią formacji.
4. Osiągnięcie limitu jednostek blokuje wyłącznie zagranie kolejnej karty jednostki. Zaklęcia nadal mogą być zagrywane, jeżeli spełniają własne warunki i gracz ma AP.
5. Nadal obowiązuje zajętość pojedynczych heksów oraz dozwolona połowa planszy. Usunięcie slotów nie oznacza usunięcia rozmieszczania jednostek na polach.
6. Nie zmieniamy kolejności faz `RoundStart -> Preparation -> Combat -> RoundResolution`, naprzemiennego startera przygotowania, obrażeń gracza po walce ani resetu jednostek między rundami.
7. AP nie ma docelowo osobnego limitu maksymalnego. W rundzie `N` pula wynosi `N` AP przy ustawieniach bazowych `1 + 1 * (N - 1)`.
8. Obecny limit talii MVP wynosi 8 kart. Limit 8 jednostek jest regułą bezpieczeństwa i przygotowaniem na większe talie, summon lub inne źródła jednostek; ten plan nie zmienia zasad budowy talii.

## 3. Stan obecny

### 3.1. AP i dobieranie

`BattleConfig` przechowuje ogólną progresję AP przez `StartingAp`, `ApIncreasePerStep`, `ApIncreaseEveryRounds` i `MaxAp`. `BattleState.StartNextRound` już przypisuje nową pulę AP zamiast dodawać ją do pozostałej wartości, więc niewykorzystane AP już przepada.

Aktualny asset `BattleConfig_MVP` ma:

- 3 AP na start;
- +1 AP co rundę;
- limit 99 AP;
- 3 karty na start;
- dobór 2 kart;
- maksymalnie 5 kart na ręce.

### 3.2. Sloty

Rosnący limit jednostek jest rozproszony między:

- polami progresji slotów w `BattleConfig`;
- `PlayerBattleState.DeploymentSlots`;
- obliczeniem w `BattleState.CalculateDeploymentSlots`;
- walidacją `UnitPlayService.ValidatePlay`;
- błędem `PlayUnitFailReason.NoDeploymentSlot`;
- tekstem i cache w `BattleUIController`;
- testami stanu rundy, zagrywania jednostek i AI;
- serializowanym polem `slotsText` oraz tekstem `Sloty 0/3` w scenie `Battle`.

AI nie ma własnej kopii reguły slotów. Szuka legalnego zagrania przez `UnitPlayService.ValidatePlay`, dlatego po zmianie wspólnego walidatora automatycznie zacznie respektować limit 10.

### 3.3. Pojemność areny

Plansza MVP ma rozmiar 5 x 6. Każda strona może wystawiać jednostki na 3 rzędach, czyli ma 15 dostępnych heksów. Stały limit 8 mieści się więc w obecnym układzie bez zmiany planszy ani algorytmu ustawiania AI.

## 4. Docelowy model danych

### 4.1. `BattleConfig`

Pozostawić reguły jako dane konfiguracyjne, ale uprościć je do rzeczywiście potrzebnego modelu:

```csharp
public int StartingAp = 1;
public int ApIncreasePerRound = 1;
public int StartingHandSize = 3;
public int MaxHandSize = 5;
public int DrawPerRound = 1;
public int MaxUnitsPerSide = 8;
```

Usunąć:

- `ApIncreaseEveryRounds`;
- `MaxAp`;
- `StartingDeploymentSlots`;
- `DeploymentSlotIncreasePerStep`;
- `MaxDeploymentSlots`;
- `DeploymentSlotIncreaseEveryRounds`.

Przy zmianie nazwy `ApIncreasePerStep` na `ApIncreasePerRound` użyć tymczasowo `FormerlySerializedAs`, aby nie zgubić wartości w innych ewentualnych assetach konfiguracji. `MaxUnitsPerSide` należy ustawić jawnie na 8 zamiast migrować stare `MaxDeploymentSlots`, ponieważ stara wartość opisuje inną regułę i w `BattleConfig_MVP` wynosi 6.

`OnValidate` powinno zapewniać:

- `StartingAp >= 0`;
- `ApIncreasePerRound >= 0`;
- `0 <= StartingHandSize <= MaxHandSize`;
- `DrawPerRound >= 0`;
- `MaxUnitsPerSide >= 1`;
- co najmniej 8 pól wystawienia na stronę dla konfiguracji MVP.

Ostatniego warunku nie należy naprawiać przez ciche zwiększanie planszy. Niespójna konfiguracja planszy i limitu powinna być widoczna podczas walidacji assetu oraz pokryta testem konfiguracji.

### 4.2. `PlayerBattleState`

Usunąć `DeploymentSlots` oraz parametr `deploymentSlots` z konstruktora. Limit jest niezmienną regułą meczu i powinien pochodzić z `BattleState.Config.MaxUnitsPerSide`, zamiast być duplikowany i mutowany osobno dla każdej strony.

Można dopasować początkową pojemność `Units` do 8 i `Hand` do 5. Nie zmienia to kontraktu kolekcji, ale ogranicza realokacje w typowej rozgrywce mobilnej.

### 4.3. Wyliczenie AP

W `BattleState` zastąpić ogólną progresję AP jednoznacznym obliczeniem:

```text
roundAp = StartingAp + (RoundNumber - 1) * ApIncreasePerRound
```

Na początku kolejnej rundy dla obu stron wykonać przypisanie:

```csharp
player.Ap = CalculateRoundAp();
```

Nie dodawać nowej wartości do `player.Ap`. To przypisanie jest kontraktem powodującym utratę niewykorzystanego AP. Obliczenie powinno defensywnie unikać przepełnienia `int`, mimo że normalny mecz zakończy się znacznie wcześniej.

Progresja `RoundDamageBonus` pozostaje bez zmian i może nadal korzystać z istniejącego ogólnego helpera.

## 5. Zmiany w logice gameplay

### 5.1. Tworzenie meczu i początek rundy

W `BattleState.Create`:

- utworzyć obie strony bez parametru slotów;
- przydzielić każdej stronie 1 AP;
- dobrać po 3 karty z zachowaniem limitu ręki 5;
- nie wykonywać dodatkowego `DrawPerRound` w rundzie 1.

W `PreparePlayerForNextRound`:

- zresetować `IsReady`;
- zastąpić pozostałe AP pulą wyliczoną dla nowej rundy;
- usunąć obliczanie i przypisywanie slotów;
- zachować reset formacji i zdrowia;
- dobrać dokładnie 1 kartę, jeżeli na ręce jest mniej niż 5 kart i talia nie jest pusta.

### 5.2. Walidacja zagrania jednostki

W `UnitPlayService.ValidatePlay` zastąpić:

```csharp
player.Units.Count >= player.DeploymentSlots
```

przez kontrolę:

```csharp
player.Units.Count >= battleState.Config.MaxUnitsPerSide
```

Zmienić `PlayUnitFailReason.NoDeploymentSlot` na domenowe `UnitLimitReached`. Zachować pozycję tej walidacji po sprawdzeniu AP i przed walidacją pola, aby nie zmieniać bez potrzeby dotychczasowego priorytetu błędów.

Nie mutować AP, ręki, `PlayedCards` ani listy jednostek przy odrzuconym jedenastym zagraniu.

### 5.3. AI

`EnemyPreparationAI` powinno nadal opierać wybór na `UnitPlayService.ValidatePlay`. Nie dodawać drugiego sprawdzenia limitu w AI.

Zweryfikować dwa ważne przypadki:

- po osiągnięciu 8 jednostek AI przestaje szukać legalnego pola dla kart jednostek i nie wpada w pętlę;
- po osiągnięciu limitu jednostek AI nadal może wybrać legalne zaklęcie z ręki.

## 6. UI i serializacja sceny

W `BattleUIController`:

- zmienić `slotsText` na `unitLimitText`;
- zmienić `shownSlots` na `shownMaxUnits`;
- pobierać limit z `state.Config.MaxUnitsPerSide`;
- wyświetlać `Jednostki {count}/8` zamiast `Sloty {count}/{slots}`;
- zachować odświeżanie tylko po zmianie liczby jednostek lub limitu, bez aktualizacji tekstu co klatkę.

Dla bezpiecznej migracji referencji sceny można użyć `[FormerlySerializedAs("slotsText")]` na `unitLimitText`, a następnie otworzyć, zweryfikować i zapisać scenę `Battle` przez Unity MCP. Zaktualizować również początkową treść TMP z `Sloty 0/3` na `Jednostki 0/8`.

Układ ręki jest już dynamiczny i korzysta z ponownie używanych `CardView`. Należy tylko zweryfikować wizualnie układ 3, 4 oraz 5 kart na typowych proporcjach telefonu i w safe area; nie jest potrzebny nowy canvas ani nowy system layoutu.

## 7. Etapy implementacji

### Etap 1 — testy docelowego kontraktu

- Zaktualizować domyślne wartości w `TestDefinitions.CreateConfig`.
- Dodać czerwone testy AP 1/2/3, utraty pozostałego AP, ręki 3/4/5 i limitu 8 jednostek.
- Zmienić testy odnoszące się do slotów na testy stałego limitu jednostek.

Kryterium odbioru: testy jednoznacznie opisują nowe zasady przed migracją implementacji.

### Etap 2 — konfiguracja i stan rundy

- Uprościć pola `BattleConfig`.
- Dodać `MaxUnitsPerSide`.
- Usunąć `DeploymentSlots` z `PlayerBattleState` i konstruktora.
- Uprościć `CalculateRoundAp` i usunąć `CalculateDeploymentSlots`.
- Zachować przypisanie AP jako jawne odrzucenie niewykorzystanej puli.

Kryterium odbioru: obie strony zaczynają rundy z AP 1, 2, 3... niezależnie od pozostałego AP i nie mają stanu slotów.

### Etap 3 — limit jednostek i AI

- Przełączyć `UnitPlayService` na `MaxUnitsPerSide`.
- Zmienić fail reason na `UnitLimitReached`.
- Zaktualizować testy zagrywania oraz AI.
- Sprawdzić zachowanie zaklęć przy pełnej arenie.

Kryterium odbioru: każda strona może mieć najwyżej 8 jednostek, dziewiąte zagranie jest atomowo odrzucane, a AI kończy przygotowanie poprawnie.

### Etap 4 — ręka i dobór

- Ustawić startowy dobór na 3.
- Ustawić dobór kolejnej rundy na 1.
- Zachować limit 5 i obecne zachowanie pełnej ręki.
- Zweryfikować brak pobrania dodatkowej karty przy wejściu do rundy 1.

Kryterium odbioru: start to dokładnie 3 karty, a każda następna runda próbuje dobrać dokładnie 1 kartę do limitu 5.

### Etap 5 — HUD, scena i assety

- Zmienić nazewnictwo i treść licznika jednostek.
- Zaktualizować `BattleConfig_MVP.asset` do wartości 1/1, 4/5/1 i 10.
- Zweryfikować oraz zapisać scenę `Battle` przez Unity MCP.
- Po migracji wyszukać pozostałe referencje do usuniętych pól i słowa `Sloty` w kodzie oraz scenie.

Kryterium odbioru: Inspector, scena i HUD nie zawierają pojęcia rosnących slotów, a wszystkie referencje Unity są poprawne.

### Etap 6 — weryfikacja całości

- Uruchomić najpierw wąskie testy Edit Mode przez Unity MCP.
- Następnie uruchomić pełny zestaw Edit Mode.
- W Play Mode rozegrać co najmniej trzy rundy dla obu wariantów startera przygotowania.
- Sprawdzić układ 5 kart oraz 8 jednostek na każdej połowie planszy.

Kryterium odbioru: brak błędów kompilacji, testy przechodzą, a pełna pętla rundy działa bez regresji faz, AI i prezentacji.

## 8. Strategia testów

### `BattleStateTests`

- runda 1 zaczyna się z 1 AP i 3 kartami po obu stronach;
- ręka początkowa nie przekracza 5;
- mała talia daje tyle kart, ile jest dostępne, bez błędu;
- tworzenie stanu nie zawiera zależności od slotów.

### `RoundFlowTests`

- po rundzie 1 obie strony otrzymują dokładnie 2 AP;
- po rundzie 2 obie strony otrzymują dokładnie 3 AP;
- pozostawienie 1 AP i wydanie całego AP prowadzi do tej samej puli następnej rundy;
- gracz z 3 kartami dobiera jedną do 4;
- gracz z 4 kartami dobiera jedną do 5;
- gracz z 5 kartami pozostaje przy 5, a niedobrana karta pozostaje w talii;
- pusta talia nie zmienia ręki;
- dobór i reset AP nie zachodzą po zakończeniu meczu;
- reset zdrowia i formacji pozostaje bez zmian.

### `DeckHandUnitPlayTests`

- zagrania od 1 do 10 są legalne przy wystarczającym AP i wolnych polach;
- dziewiąte zagranie zwraca `UnitLimitReached`;
- odrzucone zagranie nie pobiera AP i nie usuwa karty z ręki;
- limit jest niezależny dla gracza i AI;
- zaklęcie pozostaje legalne przy 10 jednostkach;
- zajęte pole nadal zwraca `TileOccupied`, kiedy limit nie został osiągnięty.

### `EnemyPreparationAITests`

- AI nie wystawia dziewiątej jednostki;
- AI przy limicie jednostek może zagrać legalne zaklęcie;
- AI bez legalnej akcji kończy przygotowanie przez `Ready`;
- wyszukiwanie pozycji nadal działa dla 8 jednostek na obszarze 15 pól.

### UI / Play Mode

- HUD pokazuje `Jednostki 0/8`, `Jednostki 8/8` i nie pokazuje `Sloty`;
- 4 i 5 kart mieści się na ekranie telefonu bez wyjścia poza safe area;
- AP widoczne w kolejnych rundach ma wartości 1, 2, 3;
- po wydaniu części AP następna runda nie dodaje puli do reszty;
- gracz i AI nie mogą wystawić więcej niż 8 jednostek;
- pełny przepływ rund i ogłoszenia rundy pozostają poprawne.

## 9. Pliki objęte zmianą

Główne pliki runtime:

- `Assets/DeckBattle/Scripts/Data/BattleConfig.cs`
- `Assets/DeckBattle/Scripts/Battle/BattleState.cs`
- `Assets/DeckBattle/Scripts/Battle/PlayerBattleState.cs`
- `Assets/DeckBattle/Scripts/Cards/UnitPlayService.cs`
- `Assets/DeckBattle/Scripts/Cards/PlayUnitFailReason.cs`
- `Assets/DeckBattle/Scripts/AI/EnemyPreparationAI.cs` — tylko jeśli testy ujawnią potrzebę korekty przepływu
- `Assets/DeckBattle/Scripts/UI/BattleUIController.cs`

Assety i scena:

- `Assets/DeckBattle/Data/Configs/BattleConfig_MVP.asset`
- `Assets/DeckBattle/Scenes/Battle.unity`

Testy:

- `Assets/DeckBattle/Tests/EditMode/TestDefinitions.cs`
- `Assets/DeckBattle/Tests/EditMode/BattleStateTests.cs`
- `Assets/DeckBattle/Tests/EditMode/RoundFlowTests.cs`
- `Assets/DeckBattle/Tests/EditMode/DeckHandUnitPlayTests.cs`
- `Assets/DeckBattle/Tests/EditMode/EnemyPreparationAITests.cs`
- `Assets/DeckBattle/Tests/EditMode/BattleUIControllerTests.cs`

## 10. Wydajność mobilna i ryzyka

Zmiana nie dotyka URP, shaderów, tekstur, overdraw ani rozmiaru buildu. Sama logika jest wykonywana przy tworzeniu meczu, rozpoczęciu rundy i walidacji akcji, a nie w `Update`.

Najważniejsze punkty profilowania i ryzyka:

- 20 jednostek jednocześnie zwiększa koszt symulacji, prezentacji, animatorów, pasków HP i statusów względem obecnego limitu 6; należy zmierzyć czas ticka walki oraz najgorszą klatkę prezentacji na urządzeniu mobilnym;
- algorytmy wyboru celu i ruchu mogą rosnąć wraz z liczbą jednostek, dlatego scenariusz 10 vs 10 powinien być częścią testu wydajnościowego;
- `EnemyPreparationAI` skanuje rękę i pola, ale przy ręce ograniczonej do 5 i 15 polach koszt pozostaje mały i deterministyczny;
- HUD powinien nadal korzystać z cache i eventu `StateChanged`, bez nowych aktualizacji per-frame;
- nie należy tworzyć dodatkowych widoków kart ponad istniejącą pulę; maksymalnie aktywnych będzie 5.

## 11. Kryteria końcowe

Plan jest zrealizowany, gdy:

1. Obie strony zaczynają mecz z 1 AP i 3 kartami.
2. W rundzie `N` obie strony otrzymują `N` AP, bez przenoszenia reszty.
3. Każda kolejna runda próbuje dobrać 1 kartę, ale ręka nigdy nie przekracza 5.
4. Każda strona może utrzymywać maksymalnie 8 jednostek.
5. Dziewiąta jednostka jest odrzucana bez częściowej mutacji stanu.
6. W modelu runtime, konfiguracji i widocznym HUD nie ma progresji slotów.
7. AI respektuje ten sam limit i nadal może zagrywać zaklęcia przy pełnej arenie.
8. Istniejący przebieg faz, reset formacji, obrażenia rundy i naprzemienny starter pozostają bez regresji.
9. Testy Edit Mode przechodzą, a scenariusz 10 vs 10 zostaje sprawdzony pod kątem stabilności klatki na profilu mobilnym.
