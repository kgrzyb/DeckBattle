# Plan: sterowanie kamerą podczas bitwy

> Aktualizacja 2026-08-13: część planu dotycząca orbitowania i limitów obrotu została zastąpiona przez pan na płaszczyźnie `XZ`. Obecna implementacja zachowuje stałą rotację startową kamery, obsługuje zoom pinch oraz ograniczone przesuwanie po `X` i `Z`.

## 1. Cel

Dodać dotykowe sterowanie kamerą w scenie `Battle`:

- obrót kamery w poziomie (`yaw`) po przesunięciu jednego palca w lewo lub prawo;
- obrót kamery w pionie (`pitch`) po przesunięciu jednego palca w górę lub dół;
- zoom gestem pinch wykonywanym dwoma palcami;
- konfigurowalne ograniczenia zbliżenia, oddalenia oraz obu osi obrotu;
- zachowanie obecnych interakcji z kartami, jednostkami, planszą i UI.

Sterowanie ma działać w całej scenie bitwy, zarówno w fazie przygotowania, jak i
podczas walki. Zmiana kamery jest wyłącznie prezentacyjna: nie może wpływać na
symulację, pozycje heksów, targetowanie ani wynik walki.

## 2. Stan obecny

- Projekt używa Unity `2022.3.62f3`, URP i klasycznego `UnityEngine.Input`
  (`activeInputHandler: 0`). Nie ma zależności od Input System ani Cinemachine.
- `BattleInputController` jest centralnym miejscem obsługi dotyku planszy. Czyta
  `Input.touchCount`/`Input.GetTouch`, obsługuje też mysz w Edytorze i używa
  `EventSystem.current.IsPointerOverGameObject` do blokowania wejścia nad UI.
- Ten sam kontroler rozróżnia tapnięcie jednostki, przytrzymanie i przeciąganie
  jednostki oraz wybrane karty. `CardView` przekazuje własne eventy pointer/drag
  do `BattleInputController`.
- Główna kamera jest kamerą perspektywiczną z FOV `30`, pozycją
  `(0, 25, -22.5)` i nachyleniem `50` stopni. Nie ma obecnie rigu ani komponentu
  sterującego kamerą.
- Raycasty planszy korzystają z referencji `battleCamera`, dlatego po zmianie
  pozycji kamery nadal będą trafiać w poprawne obiekty bez zmiany logiki planszy.
- Overlay statusów wykrywa zmianę macierzy kamery, floating damage text korzysta
  z kamery świata, a paski zdrowia obracają się w stronę `Camera.main`. Te
  systemy trzeba zweryfikować z ruchomą kamerą, ale nie powinny wymagać zmiany
  kontraktów gameplayowych.

## 3. Przyjęta semantyka kamery

Kamera orbituje wokół stałego punktu skupienia znajdującego się w wizualnym
centrum pola bitwy. Pozycja kamery jest zawsze wyliczana z trzech wartości:

```text
focusPoint + yaw + pitch + distance
```

Wynikowa poza:

```text
rotation = Quaternion.Euler(pitch, yaw, 0)
position = focusPoint - rotation * Vector3.forward * distance
```

Zoom zmienia `distance`, a nie `Camera.fieldOfView`. Zachowuje to przewidywalną
perspektywę, upraszcza limity i nie wymaga specjalnych korekt UI zależnych od
FOV.

W scenie należy dodać pusty obiekt `BattleCameraFocus` ustawiony w punkcie, wokół
którego ma obracać się kamera. Jego pozycję należy dobrać tak, aby początkowa
poza odpowiadała obecnemu kadrowi. Nie należy zakładać automatycznie, że środek
kadru jest dokładnie w `(0, 0, 0)`; obecna kamera przecina płaszczyznę planszy
nieco przed środkiem osi `Z`.

Nie dodawać przesuwania punktu skupienia (`pan`), bezwładności, kolizji kamery ani
automatycznego śledzenia jednostek w ramach tego zadania.

## 4. Konfiguracja

Na nowym komponencie `BattleCameraController` dodać serializowane pola:

```csharp
[SerializeField] private Camera controlledCamera;
[SerializeField] private Transform focusTarget;

[Header("Zoom")]
[SerializeField, Min(0.1f)] private float minDistance;
[SerializeField, Min(0.1f)] private float maxDistance;
[SerializeField, Min(0f)] private float pinchSensitivity;

[Header("Rotation")]
[SerializeField] private Vector2 pitchLimits;
[SerializeField] private Vector2 yawOffsetLimits;
[SerializeField, Min(0f)] private float horizontalSensitivity;
[SerializeField, Min(0f)] private float verticalSensitivity;
```

Znaczenie limitów:

- `minDistance` — maksymalne zbliżenie kamery;
- `maxDistance` — maksymalne oddalenie kamery;
- `pitchLimits.x/y` — minimalny i maksymalny kąt góra/dół;
- `yawOffsetLimits.x/y` — dozwolony obrót lewo/prawo względem początkowego
  kąta kamery.

Limity yaw mają być względne wobec pozycji startowej, dzięki czemu można zmienić
początkowy kadr w scenie bez ponownego przeliczania ograniczeń w wartościach
świata. Pitch może być przechowywany bezpośrednio, ponieważ musi pozostać z dala
od osobliwości przy `0` i `90` stopniach.

`OnValidate` oraz inicjalizacja runtime powinny zabezpieczać konfigurację:

- `minDistance >= 0.1f`;
- `maxDistance >= minDistance`;
- dolny limit każdej osi nie może być większy od górnego;
- pitch powinien pozostać w bezpiecznym zakresie, np. `1..89` stopni;
- czułości nie mogą być ujemne.

Wartości startowe należy dobrać w Game View na kilku proporcjach telefonu. Jako
punkt wyjścia można zachować pitch `50` stopni, dopuścić ograniczony yaw po obu
stronach i ustawić zakres odległości wokół obecnej odległości kamery od punktu
skupienia. Ostatecznych liczb nie należy zatwierdzać bez sprawdzenia, czy cały
aktywny obszar planszy pozostaje czytelny.

## 5. Podział odpowiedzialności

### `BattleCameraController`

Nowy, lekki komponent prezentacyjny w `Assets/DeckBattle/Scripts/Input`:

- przechowuje konfigurację oraz bieżące `yaw`, `pitch` i `distance`;
- odczytuje początkową pozę kamery względem `focusTarget` w `Awake`;
- przyjmuje już rozstrzygnięte delty gestów przez metody, np.
  `Orbit(Vector2 normalizedDelta)` i `Zoom(float normalizedPinchDelta)`;
- clampuje stan i ustawia transform kamery;
- nie czyta samodzielnie `Input`, nie zna kart, jednostek ani faz bitwy;
- nie wykonuje pracy w każdej klatce, gdy kamera się nie porusza.

Nie dodawać Cinemachine ani nowego Input Systemu tylko dla tej funkcji. Obecne
API Unity wystarcza, a nowa zależność zwiększyłaby zakres zmiany i ryzyko na
mobile.

### `BattleCameraOrbitState`

Wydzielić mały, niezależny od `MonoBehaviour` stan/algorytm odpowiedzialny za:

- akumulowanie yaw, pitch i distance;
- clampowanie do limitów;
- zachowanie yaw jako offsetu względem kąta początkowego;
- obliczenie pozycji i rotacji kamery.

Pozwoli to przetestować matematykę w EditMode bez uruchamiania sceny. Klasa lub
struktura nie powinna tworzyć kolekcji ani alokować pamięci podczas gestu.

### `BattleInputController`

Rozszerzyć istniejący kontroler, ponieważ już rozstrzyga własność dotyku pomiędzy
planszą, jednostkami i kartami. Dodać referencję do `BattleCameraController` oraz
tryby oczekiwania/obsługi gestu kamery. Nie tworzyć drugiego komponentu, który
równolegle odczytuje te same touche — prowadziłoby to do jednoczesnego ruchu
kamery i jednostki albo do przypadkowych tapnięć po pinch.

Kontroler wejścia odpowiada wyłącznie za:

- śledzenie `fingerId`;
- rozpoznanie tap/drag/pinch;
- ustalenie, kto jest właścicielem gestu;
- normalizację delty względem krótszego wymiaru ekranu;
- przekazanie delty do `BattleCameraController`.

## 6. Reguły rozstrzygania gestów

Gest po rozpoczęciu ma jednego właściciela aż do zakończenia. Zmiana położenia
palca nad innym UI lub obiektem nie może przejmować aktywnego gestu.

| Początek wejścia | Ruch | Wynik |
| --- | --- | --- |
| Nad UI lub kartą | dowolny | Kamera ignoruje wejście; działa UI/karta. |
| Pusta część planszy | poniżej progu | Tap planszy po puszczeniu. |
| Pusta część planszy | powyżej progu | Obrót kamery. |
| Jednostka gracza podczas przygotowania | poniżej progu | Obecne wybranie/podgląd jednostki. |
| Jednostka gracza podczas przygotowania | powyżej progu | Obecne przeciąganie jednostki, nie kamera. |
| Jednostka, której nie można przeciągać | poniżej progu | Obecny podgląd jednostki. |
| Jednostka, której nie można przeciągać | powyżej progu | Obrót kamery. |
| Dwa palce rozpoczęte poza UI | zmiana odległości | Zoom pinch. |

Szczegółowe zasady:

1. Tap pustej planszy trzeba odroczyć do puszczenia palca. Obecnie jest
   wykonywany na `TouchPhase.Began`; bez odroczenia kamera obracana jednym palcem
   wywoływałaby najpierw akcję planszy lub czyściła wybór.
2. Obrót rozpoczyna się dopiero po przekroczeniu konfigurowalnego progu ruchu.
   Do obliczania obrotu użyć ruchu od poprzedniej pozycji, nie całkowitej delty od
   początku gestu.
3. Pozioma delta zmienia yaw, pionowa pitch. Kierunek pionowy powinien zostać
   sprawdzony na urządzeniu; nie dodawać domyślnej opcji odwracania osi bez
   wymagania projektowego.
4. Pinch ma priorytet nad oczekującym tapem/obrotem kamery, ale nie przejmuje
   aktywnego przeciągania karty lub jednostki. W takim przypadku drugi palec jest
   ignorowany do zakończenia gestu gameplayowego.
5. Rozpoczęcie pinch usuwa oczekujący tap, aby puszczenie palców nie otwierało
   szczegółów jednostki ani nie wykonywało akcji planszy.
6. Podczas pinch śledzić dwa konkretne `fingerId`; nie polegać na stałej
   kolejności `Input.GetTouch(0/1)` pomiędzy klatkami.
7. Po zakończeniu pinch nie przechodzić automatycznie do obrotu pozostałym
   palcem. Nowy gest zaczyna się dopiero po zwolnieniu wszystkich palców. Usuwa
   to skok kamery wynikający ze zmiany punktu odniesienia.
8. `TouchPhase.Canceled`, utrata śledzonego palca, `OnDisable` i przejście aplikacji
   w tło muszą bezpiecznie wyczyścić stan gestu bez wykonania tapnięcia.

Na potrzeby testów w Edytorze zachować obsługę myszy:

- LPM + drag poza UI — obrót;
- kółko myszy — zoom;
- klik bez przekroczenia progu — obecny tap.

Fallback myszy nie może zmieniać docelowej semantyki dotyku.

## 7. Normalizacja i płynność

Delty palca należy normalizować przez `Mathf.Min(Screen.width, Screen.height)`,
zamiast wiązać czułość bezpośrednio z liczbą pikseli. Dzięki temu pełne
przesunięcie o podobną część ekranu daje podobny obrót na różnych
rozdzielczościach.

Pinch liczyć jako różnicę pomiędzy poprzednią i bieżącą odległością dwóch palców,
również znormalizowaną względem krótszego wymiaru ekranu. Znak delty powinien
odpowiadać naturalnej semantyce:

- rozsuwanie palców — zmniejsza `distance`, czyli przybliża;
- zsuwanie palców — zwiększa `distance`, czyli oddala.

W pierwszej wersji kamera reaguje bez bezwładności. Ogranicza to dodatkowy stan,
eliminuje ciągły `Update`, daje precyzyjną kontrolę i ułatwia profilowanie.
Ewentualne wygładzanie można dodać później po testach UX, ale nie jest częścią
tego planu.

## 8. Zmiany w scenie `Battle`

Przy implementacji zmian sceny użyć najpierw Unity MCP, zgodnie z zasadami
projektu.

1. Dodać `BattleCameraFocus` w wizualnym centrum areny.
2. Dodać `BattleCameraController` do `Main Camera` albo osobnego obiektu
   `BattleCameraRig`. Preferowany jest osobny obiekt, jeśli poprawia czytelność
   hierarchii; sama kamera może pozostać bez rodzica, ponieważ jej poza jest
   wyliczana bezpośrednio.
3. Podpiąć `Main Camera` i `BattleCameraFocus`.
4. Podpiąć kontroler kamery do `BattleInputController`.
5. Ustawić i przetestować limity w Inspectorze.
6. Nie zmieniać ustawień URP, FOV, post-processingu ani pakietów.

## 9. Testy automatyczne

Dodać `BattleCameraOrbitStateTests` w `Assets/DeckBattle/Tests/EditMode`.

Minimalny zestaw przypadków:

- inicjalizacja zachowuje startowy yaw, pitch i distance;
- yaw jest clampowany do obu limitów offsetu;
- pitch jest clampowany do dolnego i górnego limitu;
- zoom-in zatrzymuje się na `minDistance`;
- zoom-out zatrzymuje się na `maxDistance`;
- wielokrotne delty nie powodują wyjścia poza limity ani dryfu poza stan;
- zerowa delta nie zmienia pozy kamery;
- niepoprawna konfiguracja limitów jest bezpiecznie normalizowana;
- wynikowa pozycja zachowuje dokładnie zadaną odległość od focus pointu;
- yaw liczony względem początkowego kąta działa także dla startowego yaw innego
  niż zero.

Po zmianie najpierw uruchomić wąskie testy EditMode przez Unity MCP. Nie uruchamiać
EditMode tests w Unity batchmode. CLI użyć tylko jako fallback, gdy Edytor nie
jest otwarty, zgodnie z `Docs/Testing.md`.

## 10. Weryfikacja manualna na urządzeniu lub Device Simulatorze

Sprawdzić co najmniej:

1. Jednopalcowy drag obraca kamerę płynnie w czterech kierunkach.
2. Pitch i yaw zatrzymują się dokładnie na obu ograniczeniach.
3. Pinch przybliża i oddala zgodnie z limitem, bez przeskoku w pierwszej klatce.
4. Puszczenie jednego palca po pinch nie rozpoczyna obrotu ani tapnięcia.
5. Tapnięcie jednostki nadal otwiera szczegóły.
6. Przeciąganie jednostki w fazie przygotowania nadal działa i nie porusza kamerą.
7. Tap i przeciąganie kart nadal działają; pinch rozpoczęty nad UI nie porusza
   kamerą.
8. Obrót z pustej części planszy nie zagrywa karty, nie przesuwa jednostki i nie
   pozostawia highlightu hover.
9. Raycasty pól i jednostek po obrocie/zoomie trafiają zgodnie z pozycją palca.
10. Paski HP, statusy i floating damage text pozostają przy swoich jednostkach
    podczas ruchu kamery.
11. Cała plansza i istotne UI pozostają czytelne na wąskim i szerokim telefonie,
    przy obu skrajnych zoomach i obrotach.
12. Gest przerwany przez pauzę, utratę fokusu lub `TouchPhase.Canceled` nie blokuje
    kolejnych wejść.

Testy wykonać także podczas aktywnej walki z wieloma jednostkami i VFX, zwracając
uwagę na stabilność frame time.

## 11. Wydajność mobilna

- Używać `Input.GetTouch(i)`, nie `Input.touches`, aby nie tworzyć tablicy.
- Nie używać LINQ, coroutine, tweenów ani nowych kolekcji w ścieżce gestu.
- Nie wykonywać raycastu w każdej klatce aktywnego obrotu; raycast jest potrzebny
  tylko przy ustalaniu właściciela gestu lub przy istniejących akcjach planszy.
- Nie aktualizować transformu kamery, gdy delta jest zerowa i stan się nie
  zmienił.
- Nie dodawać ciągłego wygładzania w `Update` w pierwszej wersji.
- Sprawdzić w Profilerze `GC.Alloc` podczas 30 sekund ciągłego orbitowania i
  pinch — oczekiwany wynik dla nowej ścieżki to `0 B/frame`.
- Obserwować CPU `BattleInputController.Update`, liczbę raycastów i koszt
  aktualizacji world-space overlayów podczas ruchu kamery.

Zmiana nie wpływa na URP, warianty shaderów, overdraw, tekstury, build size ani
logikę symulacji. Ruch kamery może jednak ujawnić większą część areny, dlatego
skrajne limity trzeba dobrać tak, aby nie zwiększać niepotrzebnie liczby
widocznych efektów i obiektów poza planszą.

## 12. Kolejność implementacji

1. Dodać testowalny `BattleCameraOrbitState` i testy clampowania/matematyki.
2. Dodać `BattleCameraController`, inicjalizację z obecnej pozy kamery i API do
   przekazywania delty obrotu oraz zoomu.
3. Rozszerzyć `BattleInputController` o oczekujący tap, gest orbitowania i pinch
   wraz z jednoznacznym śledzeniem `fingerId`.
4. Dodać fallback myszy do szybkiej weryfikacji w Edytorze.
5. Przez Unity MCP dodać focus point i podpiąć referencje w scenie `Battle`.
6. Ustawić początkowe limity i czułości bez zmiany obecnego kadru startowego.
7. Uruchomić wąskie testy EditMode i sprawdzić stan kompilacji.
8. Wykonać pełną checklistę manualną w fazie przygotowania oraz walki.
9. Sprawdzić Profiler i dopiero wtedy dostroić czułości oraz granice.

## 13. Ryzyka

- Największym ryzykiem jest konflikt jednopalcowego obrotu z wyborem i
  przeciąganiem jednostki. Musi go rozwiązać jawna własność gestu, nie kolejność
  wywołań dwóch niezależnych komponentów.
- Przeniesienie tapu pustej planszy z `pointer down` na `pointer up` może subtelnie
  zmienić odczucie UI. Trzeba sprawdzić wybór kart, czyszczenie zaznaczenia i
  highlighty.
- Euler yaw przechodzący przez `0/360` może źle clampować wartości absolutne.
  Dlatego stan przechowuje mały offset yaw względem startu.
- Zbyt niski pitch może pokazać tło poza areną, a zbyt wysoki spłaszczyć
  czytelność jednostek i zwiększyć nakładanie overlayów.
- Zbyt duże oddalenie może pogorszyć czytelność kart/jednostek, a zbyt mocne
  zbliżenie może ucinać ważne heksy i powodować clipping na `near clip plane`.

## 14. Definicja ukończenia

Funkcja jest gotowa, gdy:

- na urządzeniu mobilnym działa obrót jednym palcem oraz zoom pinch;
- wszystkie cztery granice obrotu i obie granice odległości są konfigurowalne w
  Inspectorze i zawsze respektowane;
- początkowy kadr sceny nie zmienia się po dodaniu kontrolera;
- karty, jednostki, tapy planszy i UI nie wykonują się równocześnie z gestem
  kamery;
- raycasty i overlaye pozostają poprawne po dowolnym dozwolonym ruchu kamery;
- testy matematyki przechodzą, scena kompiluje się bez błędów, a manualna
  checklista nie wykazuje regresji;
- profilowanie gestów pokazuje brak nowych alokacji per frame i brak istotnego
  pogorszenia frame time na docelowym profilu mobilnym.
