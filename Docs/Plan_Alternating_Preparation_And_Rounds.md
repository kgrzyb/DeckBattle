# Plan refaktoru naprzemiennej fazy przygotowania i kolejności rund

Status: propozycja implementacyjna  
Data: 2026-08-05  
Zakres: przebieg rundy, tury przygotowania, AI, UI oraz prezentacja ustawienia jednostek

## 1. Cel

Refaktor ma wprowadzić jedną, jednoznaczną sekwencję przygotowania:

1. Na początku meczu deterministycznie losujemy stronę rozpoczynającą pierwszą rundę.
2. Pierwsza strona może wykonywać dowolną liczbę dozwolonych akcji przygotowania.
3. Kliknięcie `Ready` kończy jej przygotowanie i przekazuje aktywność drugiej stronie.
4. Druga strona przygotowuje się i klika `Ready`.
5. Po gotowości obu stron rozpoczyna się `Combat`.
6. W następnej rundzie kolejność rozpoczęcia jest odwrócona.
7. Układ jednostek obu stron pozostaje widoczny przez cały mecz, również podczas `RoundStart` i `Preparation`.

Przykład dla meczu, w którym pierwszą rundę zaczyna gracz:

| Runda | Pierwsza strona | Druga strona |
|---|---|---|
| 1 | Gracz | Przeciwnik |
| 2 | Przeciwnik | Gracz |
| 3 | Gracz | Przeciwnik |
| 4 | Przeciwnik | Gracz |

Losowanie ma być częścią deterministycznego stanu meczu: ten sam seed powinien zawsze wskazywać tę samą stronę rozpoczynającą.

## 2. Stan obecny i główne problemy

### 2.1. `ActivePreparationSide` nie steruje regułami

`BattleState` przechowuje `ActivePreparationSide`, ale `PreparationTurnService.CanSidePrepare` sprawdza obecnie tylko fazę i `IsReady`. W efekcie obie strony mogą przygotowywać się równocześnie.

### 2.2. Przeciwnik przygotowuje się niezależnie od aktywnej strony

`BattleController.ProgressAutomaticFlow` uruchamia AI, gdy przeciwnik nie jest gotowy, bez sprawdzenia `ActivePreparationSide`. AI może więc wykonać całe przygotowanie przed turą gracza, nawet jeśli model miałby wskazywać gracza jako pierwszego.

### 2.3. Początek każdej rundy jest na stałe przypisany do gracza

`BattleState.Create`, `BeginPreparationAfterRoundStart` i `StartNextRound` ustawiają `ActivePreparationSide` na `BattleSide.Player`. Brakuje pamięci o stronie wylosowanej na początku meczu oraz reguły odwracania kolejności w kolejnych rundach.

### 2.4. Reguły akcji nie odrzucają strony nieaktywnej

`UnitPlayService`, `SpellPlayService` i `FormationService` walidują fazę oraz `IsReady`, ale nie sprawdzają aktywnej strony przygotowania. Sama blokada przycisku w UI nie wystarczy; reguła musi być egzekwowana w warstwie gameplay.

### 2.5. Widoki przeciwnika są celowo zwalniane w przygotowaniu

`BattleController.RefreshUnits` wywołuje `HideEnemyUnitViews` podczas `RoundStart` i `Preparation`. Powoduje to znikanie ustawienia przeciwnika oraz niepotrzebne zwalnianie i ponowne pobieranie widoków i overlayów między przygotowaniem a walką.

### 2.6. Countdown omija nowe wymaganie `Ready`

Obecny countdown może oznaczyć obie strony jako gotowe i rozpocząć walkę bez `Ready` aktywnej strony. Jest to sprzeczne z docelową sekwencją opisaną powyżej.

## 3. Docelowy model stanu

### 3.1. Dane w `BattleState`

Dodać trwałą dla całego meczu właściwość:

```csharp
public BattleSide InitialPreparationSide { get; private set; }
```

Zachować:

```csharp
public BattleSide ActivePreparationSide { get; private set; }
```

Setter `ActivePreparationSide` nie powinien pozostać publiczny. Zmiana aktywnej strony powinna przechodzić przez kontrolowane API stanu lub `PreparationTurnService`, aby inne systemy nie mogły ominąć reguł fazy.

Początek przygotowania dla rundy wyznaczać bez kolejnego losowania:

```text
nieparzysta runda -> InitialPreparationSide
parzysta runda   -> strona przeciwna
```

Dzięki temu kolejność jest przewidywalna, łatwa do zapisania, odtworzenia i przetestowania.

### 3.2. Deterministyczne losowanie pierwszej strony

Do losowania użyć `DeterministicRandom` i seeda meczu. Rekomendowane jest oddzielne, domenowo rozdzielone źródło dla kolejności przygotowania, np. seed połączony ze stałą `PreparationOrderSeedSalt`.

Nie należy zużywać losowania z tego samego strumienia, który tasuje talie. Inaczej dodanie wyboru pierwszej strony zmieni kolejność kart dla wszystkich istniejących seedów i utrudni porównywanie regresji.

### 3.3. Inwarianty fazy przygotowania

Podczas `BattlePhase.Preparation` muszą obowiązywać następujące zasady:

- dokładnie jedna strona jest aktywna;
- tylko aktywna i jeszcze niegotowa strona może zagrywać karty, rzucać zaklęcia, przesuwać jednostki oraz potwierdzić `Ready`;
- zagranie karty lub przesunięcie jednostki nie zmienia aktywnej strony;
- pierwsze poprawne `Ready` ustawia gotowość strony i aktywuje przeciwnika;
- drugie poprawne `Ready` rozpoczyna `Combat`;
- nieaktywna strona nie może zmienić stanu przez wywołanie API z pominięciem UI;
- flagi `IsReady` obu stron są resetowane tylko przy rozpoczęciu kolejnej rundy.

Docelowe przejście:

```text
RoundStart
  -> Preparation(starter aktywny, obie strony not ready)
  -> Ready(starter)
  -> Preparation(druga strona aktywna, starter ready)
  -> Ready(druga strona)
  -> Combat
  -> RoundResolution
  -> RoundStart z odwróconym starterem
```

## 4. Zmiany w logice gameplay

### 4.1. `BattleState`

1. Wylosować i zapisać `InitialPreparationSide` przy tworzeniu meczu.
2. Dodać mały helper zwracający stronę przeciwną, najlepiej wspólny dla modelu i serwisu.
3. Dodać helper wyznaczający startera dla wskazanego numeru rundy.
4. W `BeginPreparationAfterRoundStart` ustawić stronę wynikającą z numeru rundy, zamiast zawsze wybierać gracza.
5. W `StartNextRound` zresetować gotowość i przygotować zasoby, ale nie nadpisywać kolejności wartością `Player`.
6. Zachować `RoundStart` jako granicę prezentacyjną; losowanie nie może zależeć od coroutine ani `Application.isPlaying`.

### 4.2. `PreparationTurnService`

Uczynić serwis jedynym miejscem odpowiedzialnym za regułę aktywnej strony.

Docelowe sprawdzenie możliwości działania:

```csharp
CanSidePrepare(state, side) =
    state != null
    && state.Phase == BattlePhase.Preparation
    && state.ActivePreparationSide == side
    && !state.GetPlayerState(side).IsReady;
```

Zastąpić rozproszone operacje gotowości jedną operacją w rodzaju:

```csharp
public static bool TryConfirmReady(BattleState state, BattleSide side)
```

Operacja powinna atomowo:

1. zweryfikować fazę, aktywną stronę i gotowość;
2. oznaczyć aktywną stronę jako gotową;
3. przekazać aktywność drugiej stronie, jeśli nie jest gotowa;
4. przejść do `Combat`, jeśli obie strony są gotowe;
5. zwrócić `false` bez mutacji dla niepoprawnego wywołania.

Istniejące wrappery `MarkPlayerReady` i `MarkEnemyReady` można zachować tylko wtedy, gdy delegują do tego samego kontraktu. Martwe lub mylące API, takie jak `CompleteActiveSideAction`, należy usunąć po migracji call-site'ów.

### 4.3. Walidacja kart, zaklęć i formacji

Zaktualizować:

- `UnitPlayService.ValidatePlay`;
- `SpellPlayService.ValidatePlay`;
- `FormationService.MoveUnit`.

Każda operacja powinna odrzucać akcję strony nieaktywnej niezależnie od stanu UI. Dodać jawny powód błędu, np. `NotActivePreparationSide`, do odpowiednich enumów fail reason. Nie mapować tego przypadku na `PlayerReady`, ponieważ są to dwa różne stany i UI może komunikować je inaczej.

Walidacja powinna zachować rozróżnienie:

- poza fazą przygotowania;
- strona już gotowa;
- nie jest to aktywna strona;
- błąd konkretnej akcji, np. brak AP lub niedozwolone pole.

### 4.4. Countdown przygotowania

W wariancie zgodnym z wymaganiem usunąć automatyczne oznaczanie obu stron jako gotowych:

- usunąć `CompletePreparationCountdown` z przejścia gameplay;
- usunąć `ShouldStartPreparationCountdown` oraz helpery używane wyłącznie przez countdown;
- usunąć coroutine i stan countdownu z `BattleController`;
- usunąć countdown z tekstu fazy;
- po weryfikacji serializacji usunąć nieużywane `PreparationCountdownSeconds` z `BattleConfig` i zapisać asset przez Unity.

Jeżeli później będzie potrzebny timeout dla PvP, powinien być osobną regułą: dotyczyć wyłącznie aktualnie aktywnej strony i wywoływać ten sam kontrakt co `Ready`. Nie powinien ustawiać gotowości obu stron jednocześnie.

### 4.5. `BattlePhase.EnemyPreparation`

Nie wprowadzać osobnej fazy dla przeciwnika. Jedna faza `Preparation` plus `ActivePreparationSide` daje prostszy model i nie uzależnia reguł od tego, czy dana strona jest graczem lokalnym, AI czy w przyszłości graczem sieciowym.

Obecnie nieużywane `BattlePhase.EnemyPreparation` można usunąć w etapie porządkowym, zachowując jawne wartości liczbowe kolejnych elementów enumu.

## 5. Orkiestracja gracza i AI

### 5.1. `BattleController.ProgressAutomaticFlow`

Przebudować warunek przygotowania tak, aby zależał od `ActivePreparationSide`:

- aktywny `Player` -> zatrzymać automatyczny przepływ i czekać na input;
- aktywny `Enemy` -> wykonać pełne przygotowanie AI, potwierdzić gotowość i ponownie ocenić fazę;
- `Combat` -> uruchomić istniejący przepływ walki.

Nie sprawdzać już tylko `!state.Enemy.IsReady`, ponieważ gotowość nie mówi, czy przeciwnik ma aktualnie prawo działać.

Po turze AI controller musi wykonać synchronizację widoków i wysłać `StateChanged`, zanim przejdzie dalej. Zapobiega to sytuacji, w której AI zmieni model, ale plansza i UI odświeżą się dopiero po kolejnej akcji gracza.

### 5.2. `ConfirmReady`

`BattleController.ConfirmReady` powinien jedynie:

1. wywołać `TryConfirmReady(state, BattleSide.Player)`;
2. wyczyścić nieaktualną selekcję przez istniejący event zmiany stanu;
3. uruchomić dalszy automatyczny przepływ;
4. odświeżyć prezentację tylko po udanej mutacji.

### 5.3. `EnemyPreparationAI`

`PrepareFormation` pasuje do nowego modelu pełnej tury jednej strony: AI może wykonać wiele akcji, a następnie potwierdzić gotowość.

Wymagane zmiany:

- AI działa tylko wtedy, gdy `CanEnemyPrepare` zwraca `true`;
- gotowość AI przechodzi przez wspólny kontrakt `TryConfirmReady`;
- AI nie może rozpocząć przygotowania, kiedy aktywny jest gracz;
- wynik uwzględnia również turę złożoną wyłącznie z zaklęć;
- usunąć lub jasno oznaczyć nieużywane `ExecuteTurn`, aby nie utrzymywać dwóch konkurencyjnych modeli tury AI.

Animowanie każdej decyzji AI nie jest częścią tego refaktoru. Można je dodać później nad tym samym kontraktem bez zmiany zasad gameplay.

## 6. Stała widoczność ustawienia przeciwnika

W `BattleController.RefreshUnits` zawsze synchronizować obie kolekcje:

```text
SyncUnitViews(state.Player.Units)
SyncUnitViews(state.Enemy.Units)
```

Usunąć `HideEnemyUnitViews` oraz zależne od fazy zwalnianie widoków i overlayów.

Efekt docelowy:

- istniejące jednostki przeciwnika są widoczne podczas przygotowania gracza;
- nowe jednostki AI pojawiają się po wykonaniu jego tury;
- ustawienie obu stron pozostaje widoczne podczas `RoundStart`;
- widoki zachowują ten sam runtime ID między przygotowaniem i walką;
- nie występuje niepotrzebne zwalnianie i ponowne pobieranie obiektów z puli;
- jednostki przeciwnika pozostają tylko do podglądu — gracz nie może ich przesuwać.

`EnsureCombatUnitViews` można pozostawić jako defensywne zabezpieczenie albo usunąć po potwierdzeniu, że ciągła synchronizacja obejmuje wszystkie ścieżki wejścia do walki.

## 7. UI i input

### 7.1. Status fazy

Rozszerzyć cache `BattleUIController` o `ActivePreparationSide` i gotowość przeciwnika, aby tekst zmieniał się tylko przy zmianie stanu.

Minimalne komunikaty:

- `Twoje przygotowanie` — gracz aktywny;
- `Przeciwnik się przygotowuje` — AI aktywne;
- `Gotowy — oczekiwanie na przeciwnika` — gracz zakończył przygotowanie;
- `Walka` — `Combat`;
- istniejące komunikaty rundy i wyniku meczu pozostają bez zmian.

Nie dodawać aktualizacji tekstu w `Update`; korzystać z istniejącego `StateChanged` i cache wartości.

### 7.2. `Ready` i karty

- `Ready` jest interaktywny tylko dla aktywnego gracza.
- Karty i przesuwanie jednostek są blokowane przez `CanPlayerPrepare`.
- Przy zmianie aktywnej strony `BattleInputController.HandleBattleStateChanged` czyści zaznaczenie oraz highlighty; obecny mechanizm można wykorzystać po poprawieniu `CanPlayerPrepare`.
- Ręka może pozostać widoczna podczas tury AI, ale jej elementy powinny jasno prezentować stan nieaktywny bez przebudowy layoutu.
- Podgląd szczegółów jednostek obu stron może pozostać dostępny; interakcje modyfikujące dotyczą wyłącznie aktywnego gracza.

## 8. Etapy implementacji

### Etap 0 — testy kontraktu przed zmianą

- Zachować testy rundy, kart, zaklęć, formacji, AI i prezentacji widoków.
- Dodać testy docelowej kolejności jako najpierw czerwone testy.
- Zanotować seed używany przez testy, aby świadomie zaktualizować oczekiwanego startera.

Kryterium odbioru: testy jednoznacznie opisują oba możliwe warianty startera oraz trzy kolejne rundy.

### Etap 1 — starter meczu i naprzemienność rund

- Dodać `InitialPreparationSide`.
- Dodać niezależne deterministyczne losowanie startera.
- Wyznaczać aktywną stronę z numeru rundy.
- Usunąć stałe przypisania `BattleSide.Player` w rozpoczęciu rundy.

Kryterium odbioru: dla jednego seeda rundy mają kolejność A/B/A, a dla seeda losującego drugą stronę B/A/B.

### Etap 2 — atomowe przejście `Ready`

- Zmienić `PreparationTurnService`.
- Zablokować działania strony nieaktywnej.
- Dodać nowe fail reasony.
- Zaktualizować `UnitPlayService`, `SpellPlayService` i `FormationService`.
- Usunąć automatyczne przełączanie po pojedynczej akcji, jeśli pozostały legacy call-site'y.

Kryterium odbioru: dowolna liczba akcji pozostawia tę samą stronę aktywną, pierwsze `Ready` przekazuje turę, drugie rozpoczyna walkę.

### Etap 3 — integracja controllera i AI

- Sterować automatycznym przepływem przez `ActivePreparationSide`.
- Uruchamiać AI tylko w jego turze.
- Po turze AI synchronizować widok i publikować zmianę stanu.
- Sprawdzić oba warianty: AI rozpoczyna rundę oraz AI przygotowuje się jako drugie.

Kryterium odbioru: controller nigdy nie wykonuje tury AI podczas aktywności gracza i nie blokuje się po zmianie kolejności.

### Etap 4 — ciągła prezentacja obu formacji

- Zawsze synchronizować widoki obu stron.
- Usunąć `HideEnemyUnitViews`.
- Zweryfikować registry, overlay HP/statusów, facing i podgląd szczegółów.

Kryterium odbioru: formacja przeciwnika nie znika między walką, wynikiem rundy, `RoundStart` i przygotowaniem.

### Etap 5 — UI i input

- Dodać tekst aktywnej strony.
- Poprawić stan `Ready` i interaktywność ręki.
- Zweryfikować czyszczenie dragów, selekcji i highlightów przy przejściu tury.
- Sprawdzić layout na telefonie i safe area bez dodawania nowego canvasa, jeśli istniejący `phaseText` wystarcza.

Kryterium odbioru: użytkownik zawsze wie, kto aktualnie przygotowuje się i nie może rozpocząć niedozwolonej interakcji.

### Etap 6 — usunięcie countdownu i legacy API

- Usunąć automatyczne `Ready` obu stron.
- Usunąć nieużywane pola, metody, coroutine i testy starego zachowania.
- Zweryfikować serializowane assety przez Unity MCP i zapisać je bez missing references.
- Usunąć nieużywane `EnemyPreparation` bez zmiany liczbowych wartości pozostałych faz.

Kryterium odbioru: istnieje jedna ścieżka przejścia z przygotowania do walki — wspólny kontrakt gotowości.

## 9. Strategia testów

### Edit Mode — `BattleState` i rundy

- ten sam seed wybiera tego samego startera;
- zestaw kontrolnych seedów obejmuje oba możliwe wyniki losowania;
- runda 1 używa wylosowanej strony;
- runda 2 używa strony przeciwnej;
- runda 3 ponownie używa strony początkowej;
- wejście z `RoundStart` do `Preparation` nie nadpisuje wyliczonego startera;
- reset rundy zeruje oba `IsReady`, ale zachowuje `InitialPreparationSide`.

### Edit Mode — `PreparationTurnService`

- tylko aktywna strona może działać;
- akcja karty, zaklęcia i przesunięcia nie kończy tury;
- `Ready` strony nieaktywnej nie mutuje stanu;
- pierwsze `Ready` aktywuje drugą stronę;
- gotowa strona nie może ponownie działać;
- drugie `Ready` ustawia `Combat`;
- brak automatycznego przejścia bez gotowości aktywnej strony.

### Edit Mode — walidatory

- `UnitPlayService` zwraca `NotActivePreparationSide`;
- `SpellPlayService` zwraca `NotActivePreparationSide`;
- `FormationService` zwraca `NotActivePreparationSide`;
- dotychczasowe błędy fazy, AP, targetu, slotu i zajętego pola nadal mają poprawny priorytet.

### Edit Mode — AI

- AI nie wykonuje akcji, gdy aktywny jest gracz;
- AI może wykonać pełną turę, gdy jest aktywne;
- AI po zakończeniu ustawia gotowość przez wspólny kontrakt;
- AI jako pierwsze przekazuje aktywność graczowi;
- AI jako drugie uruchamia `Combat`;
- tura z samymi zaklęciami i tura bez legalnych akcji również kończą się poprawnym `Ready`.

### Integracja / Play Mode

- start meczu, w którym pierwszy jest gracz;
- start meczu, w którym pierwsze jest AI;
- pełne trzy rundy z kolejnością A/B/A;
- brak możliwości dragowania karty lub jednostki podczas tury AI;
- formacja przeciwnika jest widoczna w każdej fazie;
- brak duplikatów `UnitView` i overlayów po kilku rundach;
- poprawne przejście do wyniku meczu;
- pauza/background/resume nie zmienia aktywnej strony ani gotowości.

Zgodnie z zasadami projektu testy Edit Mode należy uruchomić przez Unity MCP w już otwartym Editorze. Unity CLI jest tylko fallbackiem i nie należy uruchamiać Edit Mode w batchmode.

## 10. Wydajność mobilna

- Brak nowych `Update` i odpytywania stanu co klatkę.
- Zmiany UI i prezentacji wyłącznie po `StateChanged`.
- Brak LINQ, closure i tymczasowych kolekcji w automatycznym przepływie.
- Ciągła widoczność przeciwnika powinna zmniejszyć churn puli widoków i overlayów między fazami.
- Nie odświeżać wszystkich tekstów i layoutu przy każdej akcji AI, jeśli AI wykonuje pełną turę atomowo.
- Nie dodawać nowych pakietów, shaderów ani efektów przezroczystości.

Punkty profilowania po wdrożeniu:

- liczba aktywnych i poolowanych `UnitView` po każdej rundzie;
- GC Alloc przy `Ready`, zmianie aktywnej strony i wejściu do `Combat`;
- koszt `RefreshUnits` dla maksymalnej liczby jednostek;
- brak layout rebuildów podczas bezczynnego oczekiwania na turę gracza.

## 11. Ryzyka i zabezpieczenia

### Zmiana seeda wpływa na talie

Ryzyko: losowanie startera zużyje RNG tasowania i zmieni istniejące deterministyczne scenariusze.

Zabezpieczenie: osobny, domenowo rozdzielony strumień RNG i test zachowania kolejności talii dla kontrolnego seeda.

### Controller przejdzie przez AI i walkę w jednym wywołaniu

Ryzyko: po `Ready` gracza AI wykona turę i uruchomi walkę bez odświeżenia widoków pośrednich.

Zabezpieczenie: jawna synchronizacja prezentacji oraz `StateChanged` po turze AI, przed rozpoczęciem prezentacji walki.

### Nieaktualny drag lub highlight po zmianie tury

Ryzyko: gracz kończy turę w trakcie zaznaczenia karty lub jednostki.

Zabezpieczenie: `CanPlayerPrepare` uwzględnia aktywną stronę, a `HandleBattleStateChanged` zawsze czyści niedozwolony tryb inputu i highlighty.

### Niejawne call-site'y omijają `PreparationTurnService`

Ryzyko: test, AI lub controller ustawi `IsReady` albo `ActivePreparationSide` bez walidacji.

Zabezpieczenie: ograniczyć settery, wyszukać wszystkie zapisy i pozostawić jedną operację przejścia gotowości.

### Usunięcie countdownu z assetów

Ryzyko: stare pole pozostanie w YAML lub prefab/scena utraci referencję.

Zabezpieczenie: najpierw usunąć użycie runtime, następnie pole serializowane, sprawdzić i zapisać assety przez Unity MCP; nie edytować ręcznie GUID-ów ani plików `.meta`.

## 12. Główne pliki objęte zmianą

### Gameplay i flow

- `Assets/DeckBattle/Scripts/Battle/BattleState.cs`
- `Assets/DeckBattle/Scripts/Battle/PreparationTurnService.cs`
- `Assets/DeckBattle/Scripts/Battle/BattleController.cs`
- `Assets/DeckBattle/Scripts/Battle/RoundFlowService.cs` — tylko jeśli starter będzie ustawiany podczas rozliczenia rundy
- `Assets/DeckBattle/Scripts/AI/EnemyPreparationAI.cs`
- `Assets/DeckBattle/Scripts/Core/BattlePhase.cs`
- `Assets/DeckBattle/Scripts/Core/DeterministicRandom.cs` — bez zmiany algorytmu, ewentualnie użycie przez helper seeda

### Reguły akcji

- `Assets/DeckBattle/Scripts/Cards/UnitPlayService.cs`
- `Assets/DeckBattle/Scripts/Cards/SpellPlayService.cs`
- `Assets/DeckBattle/Scripts/Cards/PlayUnitFailReason.cs`
- `Assets/DeckBattle/Scripts/Cards/PlaySpellFailReason.cs`
- `Assets/DeckBattle/Scripts/Formation/FormationService.cs`
- `Assets/DeckBattle/Scripts/Formation/FormationMoveFailReason.cs`

### Prezentacja i input

- `Assets/DeckBattle/Scripts/UI/BattleUIController.cs`
- `Assets/DeckBattle/Scripts/Input/BattleInputController.cs`
- scena/prefab UI tylko wtedy, gdy istniejący `phaseText` nie wystarczy

### Testy

- `Assets/DeckBattle/Tests/EditMode/BattleStateTests.cs`
- `Assets/DeckBattle/Tests/EditMode/PreparationTurnServiceTests.cs`
- `Assets/DeckBattle/Tests/EditMode/RoundFlowTests.cs`
- `Assets/DeckBattle/Tests/EditMode/EnemyPreparationAITests.cs`
- `Assets/DeckBattle/Tests/EditMode/DeckHandUnitPlayTests.cs`
- `Assets/DeckBattle/Tests/EditMode/SpellPlayServiceTests.cs`
- `Assets/DeckBattle/Tests/EditMode/FormationServiceTests.cs`
- test integracyjny controllera lub Play Mode dla pełnego przepływu i widoczności

## 13. Zakres poza refaktorem

- multiplayer i transport sieciowy;
- synchronizacja zegara PvP;
- zmiana strategii AI lub balansu kart;
- animowanie pojedynczych decyzji AI;
- ukrywanie informacji innych niż ustawienie jednostek, np. ręki przeciwnika;
- przebudowa systemu walki i rozliczania obrażeń rundy;
- nowe pakiety lub cięższe efekty wizualne.

Model pozostaje jednak neutralny względem rodzaju uczestnika, dzięki czemu późniejsze AI lub gracz sieciowy mogą korzystać z tego samego `BattleSide` i kontraktu `Ready`.

## 14. Definition of Done

Refaktor jest zakończony, gdy:

- starter pierwszej rundy jest losowany deterministycznie z seeda meczu;
- starter kolejnych rund zmienia się naprzemiennie bez kolejnych losowań;
- tylko `ActivePreparationSide` może modyfikować swój stan przygotowania;
- strona może wykonać wiele akcji aż do `Ready`;
- pierwsze `Ready` przekazuje przygotowanie drugiej stronie;
- drugie `Ready` jako jedyna normalna ścieżka rozpoczyna `Combat`;
- AI wykonuje przygotowanie wyłącznie w swojej turze;
- widoki jednostek przeciwnika pozostają aktywne podczas `RoundStart`, `Preparation`, `Combat` i rozliczenia rundy;
- UI jednoznacznie pokazuje aktywną stronę i blokuje niedozwolony input;
- nie ma nieużywanych alternatywnych ścieżek gotowości ani automatycznego `Ready` obu stron;
- testy logiki i pełnego przepływu przechodzą;
- Unity nie raportuje błędów kompilacji, missing scripts ani missing references;
- profiler nie pokazuje nowych alokacji w bezczynnej fazie przygotowania ani churnu widoków między fazami.
