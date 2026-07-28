# Plan: Optymalizacja kodu mobile — etap 1 (punkty 1–4)

## Cel

Usunąć najbardziej oczywiste i niskiego ryzyka koszty CPU, GC oraz aktualizacji UI podczas bitwy:

1. ponowne niszczenie i budowanie planszy,
2. przechwytywanie danych debugowych w każdym ticku,
3. aktualizowanie pozycji wszystkich overlayów jednostek w każdej klatce,
4. wykonywanie pracy przez bezczynne `UnitView`.

Etap ma poprawić stabilność czasu klatki na urządzeniach mobilnych bez zmiany zasad walki, kolejności eventów, timingu symulacji ani zachowania inputu.

## Założenia

- Plansza nie zmienia topologii w trakcie jednej bitwy.
- `BattleSimulationFactory` przekazuje do symulacji planszę pochodzącą z aktualnego `BattleState`.
- `BattleView` musi nadal działać zarówno jako część pełnego flow kontrolowanego przez `BattleController`, jak i w trybie samodzielnym używanym przez Editor/testy.
- Debug overlay jest narzędziem developerskim i nie może generować kosztu w buildzie release.
- Overlay jednostki musi nadal podążać płynnie za poruszającą się jednostką i reagować na ruch lub zmianę parametrów kamery.
- Zmiany mają zachować istniejące pule efektów, pocisków wizualnych i overlayów.

## Poza zakresem

- Pooling logicznych pocisków i optymalizacja DOTween.
- Zmiany algorytmów targetowania, pathfindingu i rozstrzygania ruchu.
- Pooling elementów Deck Buildera.
- Dynamiczna polityka FPS.
- Zmiany assetów, shaderów, URP i ustawień jakości.
- Przebudowa architektury całej prezentacji bitwy.

## Stan początkowy i pomiar bazowy

Przed implementacją zebrać krótki pomiar w Android Development Build na urządzeniu docelowym lub zbliżonym klasą do mid-range:

- 20–30 sekund fazy przygotowania z kilkoma jednostkami,
- przejście z przygotowania do walki,
- 20–30 sekund aktywnej walki,
- przejście do następnej rundy.

Zapisać:

- medianę i 95/99 percentyl czasu głównego wątku,
- `GC.Alloc` na klatkę w stabilnym stanie,
- skok CPU i GC na początku combatu,
- koszt `Canvas.BuildBatch`, `UI.UpdateRenderer` i skryptów UI,
- koszt `BattleView.Update`, `UnitView.Update` i `UnitStatusOverlayController.LateUpdate`,
- liczbę `Instantiate`/`Destroy` podczas rozpoczęcia kolejnego combatu.

Nie używać Deep Profile do pomiaru końcowego. Można go włączyć tylko pomocniczo dla pojedynczej krótkiej próbki.

## Obszar 1: Idempotentny lifecycle planszy

### Problem

`BattleController` buduje planszę na początku bitwy, a `BattleView.BindSimulation` ponownie wywołuje budowę przed każdym combatem. `BoardPresenter.Build` usuwa wszystkie kafle i tworzy nowe obiekty. `ClearExistingTiles` przechodzi zarówno po śledzonej liście, jak i po dzieciach roota, przez co ten sam obiekt może otrzymać `Destroy` więcej niż raz.

### Docelowe zachowanie

- Pierwsze powiązanie planszy tworzy wymagane kafle.
- Ponowne powiązanie tej samej planszy lub planszy o tej samej niezmienionej topologii nie tworzy i nie niszczy GameObjectów.
- Tryb samodzielny `BattleView` nadal potrafi zbudować planszę.
- Faktyczna zmiana wymiarów/topologii wykonuje kontrolowaną przebudowę.
- Każdy istniejący kafel jest usuwany najwyżej raz.

### Proponowana implementacja

1. W `BoardPresenter` rozdzielić publiczną operację zapewnienia planszy od wymuszonej przebudowy:

   ```csharp
   public void EnsureBuilt(HexBoard sourceBoard);
   private void Rebuild(HexBoard sourceBoard);
   ```

2. `EnsureBuilt` powinno:

   - odrzucać `null` w obecny, czytelny sposób,
   - sprawdzić, czy obecny zestaw kafli jest kompletny,
   - wykonać szybki no-op, jeśli plansza jest już poprawnie zbudowana,
   - zaktualizować referencję modelu planszy bez przebudowy, jeśli topologia jest zgodna,
   - wywołać `Rebuild` tylko przy pierwszej budowie lub zmianie topologii.

3. Kryterium zgodności powinno być jawne i tanie:

   - szerokość,
   - wysokość,
   - oczekiwana liczba kafli,
   - brak `null` w śledzonej kolekcji.

   Jeżeli zablokowane pola mogą później wpływać na wygląd kafli, dodać osobny krok odświeżenia danych wizualnych zamiast pełnej przebudowy.

4. W `BattleController.StartTestBattle` i `BattleView.BindSimulation` używać `EnsureBuilt`.

5. Uprościć `ClearExistingTiles` do jednego źródła enumeracji. Najbezpieczniej przejść jeden raz po dzieciach `tileRoot`, a następnie wyczyścić `tiles` i `tileByCoord`. Nie wykonywać osobnego niszczenia tych samych obiektów przez listę.

6. Nie wprowadzać puli kafli w tym etapie, jeżeli po zmianie lifecycle przebudowa występuje tylko przy faktycznej zmianie planszy. Pooling byłby wtedy zbędną komplikacją.

### Pliki

- `Assets/DeckBattle/Scripts/Board/BoardPresenter.cs`
- `Assets/DeckBattle/Scripts/Battle/BattleController.cs`
- `Assets/DeckBattle/Scripts/Battle/BattleView.cs`
- nowy lub istniejący plik testów `BoardPresenter`

### Testy

EditMode:

- pierwsze `EnsureBuilt` tworzy dokładnie `Width * Height` kafli,
- drugie `EnsureBuilt` dla tej samej planszy zachowuje te same instancje kafli,
- powiązanie zgodnej topologicznie planszy nie tworzy nowych instancji,
- zmiana wymiarów daje poprawną liczbę kafli i poprawne koordynaty,
- czyszczenie nie pozostawia duplikatów ani osieroconych dzieci.

Manualnie:

- bezpośrednie uruchomienie sceny `Battle`,
- start bitwy przez `MainMenu`,
- przejście przez co najmniej dwie rundy,
- brak mignięcia lub znikania planszy przed combatem.

## Obszar 2: Debug snapshot bez kosztu w release

### Problem

`BattleView.UpdateSimulation` wywołuje `BattleDebugSnapshot.Capture` po każdym ticku. Metoda czyści i wypełnia słowniki na potrzeby edytorowego debug overlaya, nawet jeśli dane nie są odczytywane.

### Docelowe zachowanie

- Release build nie wykonuje `Capture` w hot path.
- Editor nadal pokazuje aktualne gizma.
- Development Build może opcjonalnie przechwytywać snapshot, ale funkcja jest domyślnie wyłączona.
- Włączenie lub wyłączenie debugowania nie zmienia symulacji.

### Proponowana implementacja

1. Dodać w `BattleView` pojedynczy wrapper:

   ```csharp
   private void CaptureDebugSnapshot(IReadOnlyList<BattleEvent> events);
   ```

2. W release wrapper powinien być pusty lub jego wywołanie ma zostać usunięte kompilacyjnie.

3. Rekomendowany wariant:

   - `UNITY_EDITOR`: snapshot aktywny,
   - `DEVELOPMENT_BUILD`: snapshot aktywny wyłącznie po włączeniu jawnej flagi developerskiej,
   - pozostałe buildy: brak wywołania `Capture`.

4. Wszystkie obecne bezpośrednie wywołania `debugSnapshot.Capture` zastąpić wrapperem, w tym inicjalizację i czyszczenie.

5. Nie usuwać klasy `BattleDebugSnapshot` ani publicznego odczytu wykorzystywanego przez `BattleDebugOverlay`.

### Pliki

- `Assets/DeckBattle/Scripts/Battle/BattleView.cs`
- opcjonalnie `Assets/DeckBattle/Scripts/Battle/BattleDebugSnapshot.cs`
- testy `BattleView`/debug snapshot, jeżeli istnieją odpowiednie punkty zaczepienia

### Testy

- istniejące testy symulacji zwracają identyczne rezultaty z włączonym i wyłączonym snapshotem,
- w Editorze debug overlay nadal pokazuje zajęte i zarezerwowane pola,
- w release/IL2CPP brak próbek `BattleDebugSnapshot.Capture` w profilerze,
- `GC.Alloc` w stabilnej walce nie rośnie po zmianie.

## Obszar 3: Aktualizacja overlayów tylko po zmianie

### Problem

`UnitStatusOverlayController.LateUpdate` przelicza pozycję każdego aktywnego overlaya i zawsze zapisuje `RectTransform.anchoredPosition`, nawet gdy jednostka, kamera i root UI pozostają nieruchome.

### Docelowe zachowanie

- Overlay poruszającej się jednostki jest aktualizowany w każdej potrzebnej klatce.
- Nieruchomy overlay nie zapisuje ponownie tej samej pozycji.
- Zmiana pozycji/rotacji/projekcji kamery wymusza aktualizację wszystkich overlayów.
- Zmiana rozmiaru ekranu, pixel rectu kamery lub geometrii roota UI wymusza aktualizację.
- Jednostka za kamerą lub usunięty target nadal poprawnie ukrywa overlay.

### Proponowana implementacja

1. Rozszerzyć `TrackedOverlay` o cache:

   - ostatnia pozycja targetu w świecie,
   - ostatnia pozycja zakotwiczona,
   - informacja, czy cache został zainicjalizowany,
   - ostatni stan widoczności.

2. W kontrolerze cache’ować stan wpływający na projekcję:

   - `worldToCameraMatrix`,
   - `projectionMatrix`,
   - `camera.pixelRect`,
   - rozmiar `RectTransform.rect`,
   - orientację/rozdzielczość ekranu, jeśli nie wynika jednoznacznie z powyższych danych.

3. Na początku `LateUpdate` obliczać jeden wspólny `cameraOrRootChanged`.

4. Dla każdego overlaya wykonywać projekcję tylko wtedy, gdy:

   - target zmienił pozycję,
   - kamera lub root się zmieniły,
   - overlay nie ma jeszcze cache,
   - poprzednio target był nieprawidłowy lub niewidoczny.

5. Po projekcji przypisywać `anchoredPosition` tylko, jeśli różnica przekracza mały próg, np. `0.01f` piksela do kwadratu. Nie stosować dużego progu, który powodowałby widoczne skoki.

6. Nie używać `Transform.hasChanged` w sposób, który zeruje flagę współdzieloną z innymi systemami. Lokalne porównanie pozycji jest bardziej przewidywalne.

7. Zachować obecny pooling oraz cache zmian HP/many w `UnitStatusOverlayView`.

### Pliki

- `Assets/DeckBattle/Scripts/Battle/UnitStatusOverlayController.cs`
- opcjonalnie `Assets/DeckBattle/Scripts/Battle/UnitStatusOverlayView.cs`
- testy UI/overlay

### Testy

EditMode:

- ponowne przeliczenie identycznego stanu nie zmienia `anchoredPosition`,
- zmiana pozycji targetu aktualizuje overlay,
- zmiana parametrów kamery aktualizuje overlay,
- target za kamerą ukrywa widok,
- powrót targetu przed kamerę ponownie pokazuje widok,
- release i ponowne pobranie widoku z puli resetują cache.

Manualnie:

- poruszające się jednostki,
- pociski śledzące ruchomy cel,
- zmiana proporcji Game View,
- zmiana orientacji/safe area, jeśli środowisko testowe to umożliwia,
- brak drżenia overlayów.

Profil:

- niższy koszt `UnitStatusOverlayController.LateUpdate` w fazie bez ruchu,
- brak ciągłego `Canvas.BuildBatch` wywołanego wyłącznie przez niezmienne pozycje.

## Obszar 4: Szybka ścieżka bezczynnego UnitView

### Problem

Każdy aktywny `UnitView` wykonuje `Update`. Nawet gdy jednostka nie porusza się i nie ma aktywnego efektu wizualnego, `UpdateVisualTimers` dochodzi do zapisu `modelRoot.localScale`.

### Docelowe zachowanie

- Bezczynna jednostka kończy `Update` natychmiast i nie zapisuje transformów.
- Ruch, flash obrażeń, pulse ataku i śmierć zachowują obecny wygląd oraz timing.
- DOTween windupu nadal działa niezależnie.
- Po zakończeniu pulse skala wraca dokładnie do `baseModelScale`.
- Bind/reuse widoku resetuje cały stan przejściowy.

### Proponowana implementacja

1. Dodać tani predykat:

   ```csharp
   private bool HasActiveFrameWork =>
       isMoving || attackTimer > 0f || damageTimer > 0f || isDying;
   ```

2. `Update` powinno natychmiast wracać, gdy predykat jest fałszywy.

3. Rozdzielić aktualizację skali od aktualizacji koloru:

   - `localScale` zmieniać tylko podczas pulse ataku lub śmierci,
   - po zakończeniu animacji wykonać pojedyncze przywrócenie skali,
   - sam timer obrażeń nie powinien zapisywać skali.

4. Nie wyłączać komponentu przez `enabled = false` w pierwszej wersji. Obecny `OnDisable` zabija sekwencję ataku, więc takie rozwiązanie wymagałoby szerszej zmiany lifecycle i zwiększałoby ryzyko regresji.

5. W `FaceWorldPosition` unikać przypisania identycznej rotacji. Wykonać zapis tylko po przekroczeniu małego progu kąta lub zmianie kierunku. Nie zmieniać reguł wyboru celu.

6. Upewnić się, że `ResetTransientState`, `SetWorldPosition`, `MoveToWorldPosition`, `PlayDamage`, `PlayAttackFire` i `PlayDeath` ustawiają wszystkie pola wymagane przez szybką ścieżkę.

### Pliki

- `Assets/DeckBattle/Scripts/Battle/UnitView.cs`
- istniejące testy `UnitViewFacingTests`
- nowe testy zachowania bezczynnego widoku

### Testy

EditMode:

- bezczynny `UnitView.Update` nie zmienia transformu modelu,
- ruch nadal dochodzi dokładnie do celu,
- kolejka ruchów zachowuje kolejność,
- pulse ataku wraca do skali bazowej,
- flash obrażeń wraca do koloru strony,
- animacja śmierci wyłącza obiekt,
- wielokrotny bind spulowanego widoku nie pozostawia timerów ani zmienionej skali,
- istniejące testy facing nadal przechodzą.

Manualnie:

- walka melee i range,
- przerwanie windupu,
- obrażenia i śmierć podczas ruchu,
- kilka następujących po sobie kroków ruchu,
- przejęcie widoków między `BattleController` i `BattleView`.

## Kolejność implementacji

1. Zebrać bazowy profil urządzenia i zapisać scenariusz pomiarowy.
2. Dodać testy idempotentnej budowy planszy.
3. Wprowadzić `BoardPresenter.EnsureBuilt` i usunąć podwójne niszczenie.
4. Zweryfikować start sceny bezpośrednio oraz przez menu.
5. Dodać warunkowe przechwytywanie debug snapshotu.
6. Dodać cache stanu kamery/targetów i dirty-check overlayów.
7. Dodać szybką ścieżkę bezczynnego `UnitView`.
8. Uruchomić najwęższe testy po każdej części.
9. Uruchomić pełny zestaw EditMode po spięciu etapu.
10. Powtórzyć identyczny profil na Androidzie i porównać wyniki.

## Weryfikacja

Zgodnie z `Docs/Testing.md`, testy EditMode uruchamiać przez Unity Editor i projektowy runner. Nie uruchamiać EditMode w batchmode.

Minimalny zestaw:

- testy `BoardPresenter`,
- `BattleViewFacingTests`,
- `UnitViewFacingTests`,
- `BattleTickLoopTests`,
- testy overlayów,
- pełny EditMode po przejściu testów wąskich.

Po zmianach sprawdzić w Unity MCP:

- brak błędów i ostrzeżeń kompilacji,
- poprawny stan sceny,
- brak nieoczekiwanych wyjątków przy dwóch pełnych rundach.

## Kryteria wydajnościowe

Etap jest zaakceptowany, gdy:

- po pierwszej budowie planszy przejście do kolejnego combatu nie wywołuje `Instantiate`/`Destroy` kafli,
- release build nie wykonuje `BattleDebugSnapshot.Capture`,
- bezczynne overlaye nie zapisują pozycji w każdej klatce,
- bezczynne `UnitView` nie zapisują skali ani pozycji,
- stabilna faza przygotowania i stabilna walka mają `0 B` lub brak nowych alokacji pochodzących z czterech zmienianych obszarów,
- 95/99 percentyl czasu klatki nie pogarsza się względem pomiaru bazowego,
- nie zmieniają się wyniki symulacji dla tych samych seedów.

Nie ustalać arbitralnego procentu poprawy przed pomiarem na urządzeniu. Ważniejsze jest usunięcie potwierdzonych skoków i stałej zbędnej pracy.

## Ryzyka

- Zbyt agresywne uznanie planszy za zgodną może pozostawić nieaktualne wizualnie kafle. Kryterium zgodności musi być jawne.
- Cache overlaya może pominąć zmianę projekcji kamery lub roota. Testy muszą obejmować zmianę rozdzielczości i parametrów kamery.
- Próg pozycji nie może powodować widocznego skokowego ruchu UI.
- Szybka ścieżka `UnitView` nie może pominąć jednorazowego przywrócenia skali/koloru.
- W aktualnym worktree istnieją lokalne zmiany w kodzie symulacji. Ten etap nie powinien ich dotykać ani odwracać.

## Definicja ukończenia

Etap jest gotowy, gdy:

- wszystkie cztery obszary zostały zaimplementowane,
- plansza nie jest przebudowywana między rundami bez potrzeby,
- debug snapshot nie obciąża release,
- overlaye i jednostki aktualizują się tylko przy realnej zmianie,
- kompilacja jest czysta,
- testy wąskie i pełne EditMode przechodzą,
- dwie pełne rundy działają poprawnie w Play Mode,
- pomiar na Androidzie potwierdza brak regresji frame time, GC i UI,
- dokumentacja wyników profilowania zawiera urządzenie, build, scenariusz i porównanie przed/po.
