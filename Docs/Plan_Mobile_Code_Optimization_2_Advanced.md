# Plan: Optymalizacja kodu mobile — etap 2 (punkty 5–8)

## Cel

Zoptymalizować pozostałe obszary wskazane w audycie:

5. powtarzalne alokacje logicznych pocisków i animacji DOTween,
6. koszt targetowania i pathfindingu wykonywanego w tickach symulacji,
7. niszczenie i ponowne tworzenie całych list Deck Buildera,
8. stałe 60 FPS także w statycznych menu.

Ten etap obejmuje zmiany o różnym poziomie ryzyka. Pooling UI i polityka FPS są głównie prezentacyjne, natomiast optymalizacja symulacji może wpłynąć na deterministyczność i kolejność zachowań jednostek. Z tego powodu każda część musi być poprzedzona pomiarem i wdrażana osobno.

## Zależności

Rekomendowane jest ukończenie etapu 1:

- `Docs/Plan_Mobile_Code_Optimization_1_Core.md`.

Przed rozpoczęciem prac w `BattleSimulation`, `BattleTickLoop`, `TargetSelector` lub `MovementResolver` należy rozstrzygnąć i zachować istniejące lokalne zmiany worktree w:

- `Assets/DeckBattle/Scripts/Battle/BattleSimulation.cs`,
- `Assets/DeckBattle/Scripts/Battle/MovementResolver.cs`,
- `Assets/DeckBattle/Tests/EditMode/MovementResolverTests.cs`.

Nie wolno nadpisywać ani cofać tych zmian podczas implementacji optymalizacji.

## Zasady implementacji

- Najpierw pomiar, potem zmiana algorytmu.
- Każdy obszar powinien być osobnym, łatwym do przejrzenia zestawem zmian.
- Nie łączyć zmian zachowania symulacji z poolingiem UI lub polityką FPS.
- Zachować identyczne wyniki i kolejność eventów dla tego samego seeda.
- Nie dodawać nowych paczek tylko na potrzeby profilowania lub poolingu.
- Preferować małe pule o jawnej własności i kontrolowanym resecie.
- Nie utrzymywać dużych pul bez limitu.

## Poza zakresem

- ECS/DOTS lub Job System.
- Wielowątkowy pathfinding.
- Wymiana DOTween w całym projekcie.
- Wirtualizacja bardzo dużych kolekcji kart, jeśli aktualne dane jej nie wymagają.
- Dynamiczna zmiana jakości URP.
- Adaptacyjna rozdzielczość i optymalizacje GPU.
- Zmiany balansu, cooldownów lub częstotliwości ticków w celu ukrycia kosztów CPU.

## Pomiar i instrumentacja

### Scenariusze

Przed każdą częścią zebrać Android Development Build:

1. walka z przewagą jednostek melee,
2. walka z maksymalną realistyczną liczbą jednostek range,
3. jednostki często zmieniające cel i obchodzące blokady,
4. otwarcie Deck Buildera i 10 kolejnych operacji dodaj/usuń,
5. 30 sekund bezczynnego Main Menu,
6. aktywna interakcja dotykowa w menu i w walce.

### Markery

Jeżeli domyślne próbki profilera nie rozdzielają kosztów wystarczająco dobrze, dodać statyczne `ProfilerMarker` dla:

- pierwszego `RefreshTargets`,
- `ProjectileResolver.ResolveProjectiles`,
- `AttackCycleResolver.Resolve`,
- drugiego `RefreshTargets`,
- `MovementResolver.ResolveMovement`,
- przebudowy/rebindu list Deck Buildera.

Markery nie mogą tworzyć nazw ani stringów w runtime. Używać statycznych nazw i `using`/`Auto()` bez danych dynamicznych.

### Metryki

- `GC.Alloc` na atak range,
- `GC.Alloc` na rozpoczęcie animacji ataku,
- koszt CPU obu `RefreshTargets`,
- liczba odwiedzonych pól/pathfindingów na tick, jeżeli potrzebny jest dodatkowy licznik developerski,
- skok CPU/GC po kliknięciu karty w Deck Builderze,
- frame time i pobór energii/temperatura w bezczynnym menu,
- responsywność dotyku przy 30 FPS.

## Obszar 5A: Pooling logicznych stanów pocisków

### Problem

Każdy atak dystansowy tworzy nowy `ProjectileRuntimeState`. Obiekt jest przechowywany do momentu trafienia, a następnie usuwany z listy. Przy częstych atakach generuje to regularne alokacje i późniejsze kolekcje GC.

### Decyzja implementacyjna

Preferowany jest mały pool obiektów należący do `BattleSimulation`, ponieważ:

- obecne API i resolvery używają referencji do `ProjectileRuntimeState`,
- zmiana klasy na struct zwiększa ryzyko niezamierzonych kopii i problemów z mutacją `LastKnownTargetHex`,
- własność i moment zwrotu pocisku są jasno określone przez symulację.

### Proponowana implementacja

1. Zmienić pola tylko do odczytu w `ProjectileRuntimeState` na właściwości z prywatnym lub wewnętrznym setterem.

2. Dodać kontrolowane metody lifecycle:

   ```csharp
   internal void Initialize(...);
   internal void ResetForPool();
   ```

3. W `BattleSimulation` utrzymywać:

   - listę aktywnych pocisków,
   - mały stos wolnych stanów,
   - opcjonalny limit puli wynikający z maksymalnej realistycznej liczby jednoczesnych pocisków.

4. `SpawnProjectile` pobiera stan z puli lub tworzy nowy tylko przy rozszerzaniu capacity.

5. `RemoveProjectileAt` przed usunięciem resetuje i zwraca obiekt do puli.

6. Reset musi usunąć referencje do definicji i wyzerować identyfikatory, żeby obiekt poza aktywną listą nie wyglądał na poprawny stan runtime.

7. Nie zwracać obiektu do puli, dopóki wszystkie eventy korzystające z jego danych nie zostały utworzone.

### Testy

- drugi pocisk po zwolnieniu może użyć tej samej instancji, ale otrzymuje komplet nowych danych,
- brak przecieku `LastKnownTargetHex`, obrażeń i flagi critical między użyciami,
- dwa jednoczesne pociski nie współdzielą aktywnej instancji,
- eventy zachowują dane pocisku po jego zwrocie do puli, ponieważ `BattleEvent` przechowuje wartości,
- identyczny seed daje identyczny rezultat i kolejność eventów,
- brak `GC.Alloc` po rozgrzaniu puli w scenariuszu powtarzalnych ataków range.

## Obszar 5B: Reużycie animacji ataku

### Problem

`UnitView.PlayAttackSequence` buduje nową sekwencję DOTween dla każdego windupu i przypina przechwytującą lambdę. To może generować regularne alokacje zależne od częstotliwości ataków.

### Preferowana implementacja

Zbudować jedną znormalizowaną sekwencję na instancję `UnitView`:

1. Utworzyć sekwencję raz po inicjalizacji modelu:

   - czas bazowy 1 sekunda,
   - fazy 25%, 35%, 40%,
   - `SetAutoKill(false)`,
   - `Pause`,
   - target ustawiony na `modelRoot`.

2. Przy windupie:

   - ustawić aktualną rotację bazową,
   - zaktualizować wartości początkowe/końcowe w bezpieczny sposób albo odtworzyć tween tylko wtedy, gdy prefab/model faktycznie się zmienił,
   - dobrać `timeScale` lub inną właściwość tak, aby całkowity czas odpowiadał żądanemu duration,
   - wykonać `Restart`.

3. Nie używać lambdy tworzonej per atak. Callback powinien być cache’owany lub lifecycle ma być obsłużony jawnie.

4. `OnDisable` powinno zatrzymywać i przewijać sekwencję do stanu początkowego, a `OnDestroy` może ją ostatecznie zabić.

5. Jeżeli DOTween nie pozwala bezpiecznie zmieniać wartości sekwencji po inicjalizacji, alternatywą jest mała proceduralna animacja rotacji w istniejącym aktywnym ticku `UnitView`. Nie tworzyć nowego globalnego systemu animacji tylko dla tego przypadku.

6. Osobno zmierzyć `RoundAnnouncementView`. Optymalizować jego lambdy/sekwencje tylko wtedy, gdy profiler pokazuje istotny koszt; animacja występuje rzadko.

### Testy

- powtarzane ataki nie tworzą kolejnych sekwencji,
- różne cooldowny/windupy zachowują poprawny czas prezentacji,
- cancel windupu przywraca bazową rotację,
- śmierć i pooling jednostki zatrzymują tween,
- rebind widoku nie uruchamia starego callbacku,
- `PlayAttackFire` nadal może poprawnie dokończyć aktywną sekwencję,
- po rozgrzaniu brak alokacji zarządzanej per atak po stronie `UnitView`.

## Obszar 6: Targetowanie i pathfinding

### Problem

`BattleTickLoop.Tick` odświeża targety przed i po rozstrzygnięciu ataków. Dla wielu jednostek oznacza to wielokrotne budowanie zbiorów zajętych pól i wyszukiwanie ścieżek, mimo że workspace’y eliminują większość alokacji.

### Zasada bezpieczeństwa

Nie wdrażać cache ścieżek ani pomijania drugiego refreshu tylko na podstawie code review. Najpierw profiler musi potwierdzić, że ten obszar jest istotnym kosztem na docelowej liczbie jednostek.

### Krok 1: Warunkowy drugi refresh

Najmniejsza potencjalna optymalizacja:

1. Zachować wynik `ProjectileResolver.ResolveProjectiles`.
2. Zachować `CombatResolutionResult` z `AttackCycleResolver`.
3. Określić jawnie, które rezultaty między pierwszym target refresh a ruchem mogą unieważnić:

   - żywotność celu,
   - zajętość pola,
   - rezerwacje ruchu,
   - dozwoloną fazę ataku/ruchu,
   - aktywne efekty specjalne wpływające na targeting.

4. Drugi `RefreshTargets` wykonywać tylko wtedy, gdy wystąpiła taka zmiana.

Najbardziej prawdopodobnym minimalnym warunkiem są zgony po atakach melee, ale nie należy tego przyjmować bez przeglądu wszystkich efektów specjalnych i testów.

### Krok 2: Cache wyboru celu i planu

Wdrażać wyłącznie jeśli krok 1 nie wystarcza:

1. Dodać do symulacji rewizję stanu wpływającego na nawigację:

   - zmiana zajętości,
   - rozpoczęcie/zakończenie ruchu,
   - śmierć jednostki,
   - zmiana celu lub targetowalności.

2. Cache per jednostka powinien zawierać:

   - id celu,
   - pozycję startową,
   - planowaną pozycję celu,
   - następny krok,
   - rewizję, dla której wynik jest poprawny.

3. Cache unieważniać konserwatywnie. Fałszywe unieważnienie kosztuje CPU; brak unieważnienia może zmienić wynik walki.

4. Nie przechowywać pełnych ścieżek, jeśli ruch potrzebuje tylko następnego kroku.

5. Zachować stabilne tie-breakery i kolejność iteracji. Nie zastępować obecnych kolekcji strukturami o niedeterministycznej kolejności.

### Testy regresji symulacji

- identyczny seed daje identyczną sekwencję `BattleEvent`,
- wyniki walki, liczba ticków i zwycięzca są identyczne przed/po,
- śmierć celu podczas tego samego ticku powoduje poprawny retarget,
- pocisk zabijający cel przed fazą ataków jest uwzględniany,
- melee zabijające cel przed ruchem unieważnia odpowiedni wybór,
- ruch podczas winddown zachowuje aktualne lokalne zmiany i testy,
- jednostki nie wchodzą na zarezerwowane pola,
- tie-breakery wyboru celu i pola pozostają stabilne,
- testy na kilku stałych seedach i układach planszy przechodzą.

### Kryterium wdrożenia

Zmiana algorytmiczna trafia do projektu tylko wtedy, gdy:

- profiler wskazuje targetowanie/pathfinding jako istotny udział czasu głównego wątku,
- wynik jest mierzalnie lepszy na urządzeniu,
- pełny zestaw testów deterministycznych przechodzi,
- porównanie eventów dla ustalonych seedów nie wykazuje różnic.

## Obszar 7: Pooling elementów Deck Buildera

### Problem

Każde dodanie lub usunięcie karty wywołuje pełny `Refresh`, niszczy dzieci obu list i instancjuje nowe `DeckBuilderCardItemView`. Powoduje to skok CPU, GC oraz przebudowę layoutu/canvasu.

### Docelowe zachowanie

- Otwarcie listy tworzy tylko brakujące elementy.
- Kolejne operacje dodaj/usuń ponownie wykorzystują istniejące widoki.
- Nieaktywne elementy trafiają do ograniczonej puli.
- Listener przycisku jest dodany dokładnie raz.
- Lista kolekcji i decku zachowuje obecną kolejność.

### Proponowana implementacja

1. W `DeckBuilderController` utrzymywać osobne kolekcje:

   - aktywne widoki decku,
   - aktywne widoki kolekcji,
   - wolne widoki lub dwie pule, jeśli rooty mają różne wymagania.

2. Zastąpić `ClearChildren` operacją `ReleaseViews`:

   - `Bind(null...)` lub jawny `Release`,
   - wyzerowanie callbacku i `cardId`,
   - `SetActive(false)`,
   - zwrot do puli.

3. `AcquireView`:

   - pobiera widok z puli,
   - ustawia poprawnego parenta,
   - aktywuje,
   - wykonuje pełny bind.

4. Nie dodawać listenerów w każdym `Bind`. Obecny listener `HandleClick` powinien pozostać związany z komponentem raz przez lifecycle, a `Bind` ma tylko zmieniać dane/callback.

5. Pierwsza wersja może nadal logicznie przebindować całą małą listę, ale bez `Instantiate`/`Destroy`. Aktualizacja tylko pojedynczego elementu jest kolejnym krokiem, jeśli profiler pokaże koszt samego rebindu/layoutu.

6. Nie implementować scroll virtualization dla obecnej małej kolekcji. Rozważyć ją dopiero przy znacznie większym katalogu.

7. Ograniczyć pulę do sensownego maksimum wynikającego z rozmiaru decku i kolekcji. Nie pozwalać jej rosnąć bez limitu po zmianach danych.

### Testy

- wielokrotne `Refresh` nie zwiększa liczby instancji po rozgrzaniu,
- add/remove wywołuje callback dokładnie raz,
- rebind nie pozostawia starego `cardId` ani callbacku,
- elementy nieaktywne nie reagują na kliknięcia,
- kolejność kart jest zgodna z profilem,
- stan `interactable` i `CardVisualState` są aktualne,
- zamknięcie i ponowne otwarcie Deck Buildera nie duplikuje listenerów,
- po rozgrzaniu operacje add/remove nie generują `Instantiate`/`Destroy`.

## Obszar 8: Polityka FPS i bateria

### Problem

`MobileFrameRateBootstrap` ustawia globalnie 60 FPS dla Android/iOS. Statyczne Main Menu i Deck Builder renderują z takim samym limitem jak aktywna walka, co może niepotrzebnie zwiększać zużycie energii i temperaturę urządzenia.

### Docelowa polityka

Konserwatywny wariant:

- Main Menu: 30 FPS,
- Deck Builder: 30 FPS,
- aktywna scena Battle: 60 FPS,
- pauza/background: niski limit lub pozostawienie throttlingu systemowi,
- po powrocie z backgroundu przywrócenie limitu właściwego dla aktualnego kontekstu.

Nie obniżać automatycznie walki do 30 FPS. Nie przełączać limitu co kilka sekund bez potrzeby, aby nie powodować niestabilnego frame pacingu.

### Proponowana implementacja

1. Zastąpić stały bootstrap małą, centralną polityką:

   ```csharp
   public enum MobileFrameRateMode
   {
       Menu,
       Battle
   }

   public static void Apply(MobileFrameRateMode mode);
   ```

2. Polityka odpowiada wyłącznie za:

   - `QualitySettings.vSyncCount = 0` na mobile,
   - ustawienie `Application.targetFrameRate`,
   - zapamiętanie aktywnego trybu na potrzeby resume.

3. `MainMenuController` ustawia tryb `Menu`.

4. `BattleController` lub dedykowany mały scene bootstrap ustawia tryb `Battle`.

5. Obsłużyć `OnApplicationPause`/`OnApplicationFocus` w jednym komponencie lifecycle:

   - nie wykonywać logiki symulacji podczas pauzy,
   - po resume przywrócić tryb sceny,
   - nie tworzyć kilku konkurujących komponentów ustawiających FPS.

6. Nie używać `OnDemandRendering.renderFrameInterval` w pierwszej wersji. Łączenie go z `Application.targetFrameRate` komplikuje frame pacing i input. Rozważyć dopiero po osobnym pomiarze.

7. W Editorze nie wymuszać mobilnego limitu, chyba że jawnie uruchomiono tryb testu.

### Testy

EditMode dla czystej logiki:

- `Menu` mapuje się na 30,
- `Battle` mapuje się na 60,
- nieobsługiwany stan ma bezpieczny fallback,
- resume przywraca ostatni aktywny tryb.

Manualnie na urządzeniu:

- Main Menu stabilnie utrzymuje oczekiwany limit,
- Deck Builder pozostaje responsywny podczas scroll/tap,
- Battle wraca do 60 FPS,
- przejścia scen nie pozostawiają starego limitu,
- background/resume nie blokuje inputu i nie przyspiesza symulacji,
- brak częstego przełączania 30/60 podczas jednej sceny.

### Kryterium akceptacji

- niższy średni czas aktywności CPU/GPU lub niższa temperatura/pobór energii w 5-minutowym bezczynnym menu,
- brak zauważalnego pogorszenia dotyku i scrollowania,
- stabilny frame pacing zamiast oscylacji limitu.

## Kolejność implementacji

1. Rozstrzygnąć istniejące lokalne zmiany w symulacji i zapisać bazowy commit/stan odniesienia.
2. Zebrać profil wszystkich scenariuszy i dodać tylko potrzebne statyczne markery.
3. Wdrożyć pooling Deck Buildera jako zmianę niezależną od symulacji.
4. Zweryfikować GC/layout po wielokrotnych operacjach add/remove.
5. Wdrożyć pooling logicznych pocisków.
6. Wdrożyć reużycie animacji ataku `UnitView`.
7. Porównać `GC.Alloc` dla walk range przed/po.
8. Wdrożyć centralną politykę FPS i zmierzyć menu oraz Battle na urządzeniu.
9. Ocenić koszt targetowania/pathfindingu na docelowym limicie jednostek.
10. Jeśli koszt jest istotny, najpierw wdrożyć warunkowy drugi target refresh.
11. Dopiero jeśli nadal jest potrzebne, rozważyć cache planu/rewizję nawigacji.
12. Po każdej zmianie symulacji porównać eventy i wyniki dla stałych seedów.
13. Uruchomić pełny zestaw EditMode oraz manualny test kilku rund.

## Weryfikacja

Testy uruchamiać przez Unity Editor zgodnie z `Docs/Testing.md`. Nie uruchamiać EditMode w batchmode.

Minimalne grupy:

- `ProjectileResolverTests`,
- `AttackCycleResolverTests`,
- `BattleTickLoopTests`,
- `TargetSelectorTests`,
- `AttackPositionSelectorTests`,
- `MovementResolverTests`,
- `BattleSimulationTests`,
- testy `UnitView`,
- nowe testy puli Deck Buildera,
- testy polityki frame rate,
- pełny zestaw EditMode na końcu.

Po zmianach sprawdzić:

- brak błędów i ostrzeżeń kompilacji,
- brak różnic deterministycznych dla wybranych seedów,
- brak wzrostu pamięci po wielu rundach i wielokrotnym otwieraniu Deck Buildera,
- poprawne czyszczenie pul przy zmianie sceny,
- zachowanie po background/resume.

## Kryteria wydajnościowe

Etap jest zaakceptowany, gdy:

- po rozgrzaniu logiczne pociski nie generują alokacji per strzał,
- po rozgrzaniu animacja ataku nie tworzy nowej sekwencji/delegata per atak,
- Deck Builder nie wykonuje `Instantiate`/`Destroy` po każdym add/remove,
- menu działa z niższym limitem FPS, a walka przywraca 60 FPS,
- każda wdrożona optymalizacja pathfindingu ma potwierdzony pomiar przed/po,
- wyniki, tick count i eventy symulacji pozostają identyczne dla ustalonych seedów,
- pule nie rosną bez ograniczenia,
- 95/99 percentyl czasu klatki i `GC.Alloc` nie pogarszają się w żadnym scenariuszu.

## Ryzyka

- Niepełny reset `ProjectileRuntimeState` może przenosić dane między strzałami.
- Zbyt wczesny zwrot pocisku do puli może uszkodzić dane używane przez późniejszy kod.
- Reużywana sekwencja DOTween może zachować callback lub stan z poprzedniego bindu.
- Pominięcie target refreshu może zmienić ruch, cel lub kolejność eventów.
- Cache pathfindingu może stać się nieaktualny po rezerwacji pola przez inną jednostkę.
- Pool UI może duplikować listenery lub zachować stary callback.
- Limit 30 FPS może pogorszyć scrollowanie i odczuwalny input lag na części urządzeń.
- Zmiany symulacji nachodzą na obecne lokalne modyfikacje `BattleSimulation` i `MovementResolver`; wymagają ostrożnego scalenia.

## Definicja ukończenia

Etap jest gotowy, gdy:

- każdy z punktów 5–8 został osobno zmierzony i zaimplementowany albo świadomie odrzucony na podstawie profilu,
- pooling pocisków i animacji usuwa potwierdzone alokacje bez regresji,
- Deck Builder ponownie wykorzystuje widoki,
- polityka FPS ogranicza koszt bezczynnych menu,
- optymalizacja targetowania została wdrożona tylko jeśli była potrzebna,
- wszystkie testy deterministyczności i EditMode przechodzą,
- kilka pełnych meczów działa poprawnie na urządzeniu,
- background/resume nie uszkadza stanu bitwy,
- raport przed/po zawiera urządzenie, build, scenariusze, frame time, `GC.Alloc` i obserwacje dotyczące temperatury/baterii.
