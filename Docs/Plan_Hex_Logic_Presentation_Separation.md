# Plan rozdzielenia logiki hexów od prezentacji

## 1. Cel

Rozdzielić trzy odpowiedzialności, które obecnie są skupione w
`HexBoard`, `BoardPresenter` i `HexTileView`:

1. reguły planszy i współrzędnych używane przez gameplay;
2. mapowanie współrzędnej hexa na pozycję w świecie;
3. renderowanie i podświetlanie pól przygotowania.

Docelowo:

- gameplay nie zależy od `GameObject`, colliderów ani rendererów;
- pozycjonowanie jednostek, pocisków i efektów działa niezależnie od tego, czy
  sprite'y hexów są widoczne;
- `HexTileView` jest wyłącznie lekkim widokiem opartym na `SpriteRenderer`;
- wszystkie zmiany koloru są wykonywane przez `SpriteRenderer.color`;
- tworzone są widoki wszystkich pól planszy;
- sprite'y i ich collidery są aktywne tylko w `BattlePhase.Preparation`;
- nie powstają nowe `Update`, materiały per instancja ani alokacje przy zmianie
  fazy lub podświetlenia.

## 2. Zatwierdzone założenia

Plan opiera się na następujących wymaganiach:

1. „Tylko w fazie przygotowania” oznacza całe `BattlePhase.Preparation`, także
   czas, w którym `ActivePreparationSide == BattleSide.Enemy`.
2. W fazie przygotowania widoczne są wszystkie pola planszy — zarówno strefa
   gracza, jak i strefa przeciwnika.
3. Poza przygotowaniem obiekty widoków pozostają utworzone, ale ich wspólny root
   jest nieaktywny. Nie są niszczone i odbudowywane przy każdej rundzie.
4. Widoczność pola nie zmienia zasad deploymentu: gracz może wystawiać i
   przesuwać jednostki wyłącznie na polach dozwolonych przez istniejącą logikę.
5. Zaklęcia przygotowania nadal wskazują jednostki gracza przez odpowiadające im
   pole lub `UnitView`; zakres tego refaktoru nie zmienia reguł targetowania.

## 3. Stan obecny

### `HexBoard`

- przechowuje topologię, walkability, strefy wystawiania, sąsiedztwo, dystans i
  pathfinding;
- przechowuje również `HexSize` wykorzystywany w symulacji;
- zawiera `ToLocalPosition`, czyli obliczenie należące do przestrzeni
  prezentacji i zależne od `UnityEngine.Vector3` oraz `Mathf`.

### `BoardPresenter`

- tworzy widok dla każdego pola obu stron;
- mapuje `HexCoord` na pozycję świata używaną przez jednostki, pociski, VFX i
  floating damage text;
- przechowuje widoki i zarządza highlightami;
- sam wywołuje `UnitPlayService.ValidatePlay`,
  `SpellPlayService.ValidatePlay` oraz reguły strefy wystawiania;
- zna `BattleState`, `PlayerBattleState`, karty i jednostki runtime, przez co
  prezentacja jest sprzężona z logiką rozgrywki.

### `HexTileView`

- ma pola kolorów dla strony gracza, przeciwnika i neutralnej;
- steruje `MeshRenderer` przez `MaterialPropertyBlock`;
- nie korzysta z istniejącego aktywnego `SpriteRenderer` w `PF_HexTile`;
- łączy identyfikację pola, wybór koloru bazowego i wykonanie renderowania.

### Input i lifecycle

- `BattleInputController` raycastuje collidery `HexTileView` podczas zagrywania
  kart i przesuwania jednostek;
- poza przygotowaniem input jest blokowany przez
  `PreparationTurnService.CanPlayerPrepare`, ale widoki i collidery hexów nadal
  istnieją;
- `BoardPresenter.GetWorldPosition` jest potrzebne także w `Combat`, dlatego
  nie można wyłączać całego obiektu odpowiedzialnego za przestrzeń planszy;
- produkcyjny `PF_HexTile` ma już `SpriteRenderer` i `BoxCollider`, ale zawiera
  także wyłączone elementy starej prezentacji mesh;
- `PF_HexTile.prefab` ma obecnie lokalne niezacommitowane zmiany, które podczas
  implementacji trzeba zachować i zweryfikować przed migracją assetu.

## 4. Docelowy podział odpowiedzialności

```text
HexBoard (czysta logika planszy)
  - Contains / IsWalkable / deployment / distance / pathfinding
  - Width / Height / HexSize
                    |
                    v
HexBoardLayout (mapowanie prezentacyjne, bez MonoBehaviour)
  - HexCoord -> local Vector3
  - local center planszy
                    |
                    v
BoardPresenter (fasada przestrzeni świata)
  - local -> world przez Transform
  - GetWorldPosition / GetWorldCenter
  - delegowanie lifecycle widoku przygotowania
                    |
                    v
PreparationHexGridView
  - wszystkie pola planszy
  - widoczność rootu
  - lookup coord -> HexTileView
  - zastosowanie gotowych stanów wizualnych
                    |
                    v
HexTileView
  - Coord
  - SpriteRenderer
  - SetVisualState
```

Decyzja, czy pole jest legalne, zablokowane lub wybrane, pozostaje poza
`PreparationHexGridView` i `HexTileView`. Te klasy otrzymują wyłącznie gotowy
stan wizualny.

## 5. Model logiki planszy i geometrii

### 5.1. `HexBoard`

Zachować w `HexBoard`:

- rozmiar i granice planszy;
- `HexSize`, ponieważ obecna symulacja używa go również do przeliczenia
  dystansu walki;
- walkability, sąsiedztwo, zasięg, dystans, pathfinding i strefy wystawiania.

Usunąć z `HexBoard`:

- `ToLocalPosition`;
- zależność od `UnityEngine`, jeśli po przeniesieniu geometrii nie pozostanie
  inne jej użycie.

Nie zmieniać w tym refaktorze sposobu zapisu współrzędnych, kierunków sąsiadów,
algorytmu pathfindingu ani semantyki `HexSize`.

### 5.2. `HexBoardLayout`

Dodać małą, niemutowalną klasę lub strukturę po stronie prezentacji planszy.
Powinna otrzymać `Width`, `Height` i `HexSize`, a następnie udostępnić:

```text
Vector3 GetLocalPosition(HexCoord coord)
Vector3 GetLocalCenter()
bool Matches(HexBoard board)
```

Do niej przenieść bez zmiany wyniku istniejącą matematykę z
`HexBoard.ToLocalPosition`. Klasa nie tworzy obiektów Unity, nie przechowuje
widoków i nie zna fazy bitwy.

`GetLocalPosition` musi działać dla każdego poprawnego pola, również po stronie
przeciwnika i wtedy, gdy żaden sprite hexa nie jest aktywny.

## 6. Prezentacja pól przygotowania

### 6.1. Stan wizualny

Dodać enum wyłącznie prezentacyjny, na przykład:

```text
PreparationHexVisualState
  Default
  Legal
  Blocked
  Selected
```

Nie umieszczać w nim informacji gameplayowych takich jak karta, zajętość,
strona lub fail reason. Warstwa wyżej mapuje wynik reguł na jeden z tych stanów.

### 6.2. `HexTileView`

Przebudować `HexTileView` do kontraktu:

```text
Initialize(HexCoord coord)
SetVisualState(PreparationHexVisualState state)
```

Widok powinien:

- mieć serializowaną referencję `SpriteRenderer`;
- defensywnie znaleźć `SpriteRenderer` w dzieciach w `Awake`, jeżeli referencja
  nie jest przypisana;
- przechowywać cztery kolory odpowiadające stanom wizualnym;
- ustawiać wyłącznie `spriteRenderer.color`;
- nie używać `MeshRenderer`, `MaterialPropertyBlock`, `_BaseColor`, instancji
  materiału ani zmiany shared material;
- nie znać `BattleSide`, `BattleState`, kart, jednostek ani reguł legalności;
- nie zawierać `Update`.

Widok nie powinien sam interpretować `BattleSide`. Użyć jednego `defaultColor`
dla niepodświetlonego pola. Jeżeli w przyszłości potrzebne będzie wizualne
rozróżnienie stref, warstwa wyżej powinna przekazać jawny stan prezentacyjny;
nie należy ponownie przenosić reguł deploymentu do `HexTileView`.

### 6.3. `PreparationHexGridView`

Wydzielić z obecnego `BoardPresenter` obsługę kolekcji widoków:

- prefab i root widoków;
- lista aktywnych `HexTileView`;
- lookup `HexCoord -> HexTileView` o stałej pojemności;
- budowa i przebudowa przy zmianie topologii;
- `SetVisible(bool)`;
- `TryGetTile`, `SetState`, `ResetAllStates` i opcjonalnie
  `SetSingleHoverState`.

Budowanie ma instancjonować każdą poprawną współrzędną planszy:

```text
for r = 0 .. board.Height - 1
  for q = 0 .. board.Width - 1
    create HexTileView(HexCoord(q, r))
```

Dla produkcyjnej planszy `5 x 8` oznacza to 40 widoków.

Zmiana fazy nie może wywoływać `Instantiate` ani `Destroy`. Widoczność należy
zmieniać przez wspólny root, co jednocześnie wyłączy sprite'y i collidery.
Przed ukryciem wyczyścić stan hover/selection, aby po kolejnej rundzie nie
odtworzyć starego highlightu.

### 6.4. `BoardPresenter`

Pozostawić `BoardPresenter` jako cienką fasadę przestrzeni planszy, aby
ograniczyć migrację licznych call-site'ów prezentacji walki.

Powinien odpowiadać za:

- związanie `HexBoardLayout` z bieżącym `HexBoard`;
- `GetWorldPosition` i `GetWorldCenter` przez własny `Transform`;
- przekazanie planszy do `PreparationHexGridView`;
- przekazanie widoczności i gotowych stanów wizualnych do grid view.

Usunąć z niego metody przyjmujące `BattleState`, `PlayerBattleState`,
`RuntimeUnit` i `CardRuntimeState`, w szczególności obecną walidację kart,
zaklęć i formacji. `BoardPresenter` nie powinien wywoływać serwisów gameplay.

## 7. Wyznaczanie highlightów poza widokiem

Najmniejsza zmiana zgodna z podziałem odpowiedzialności to pozostawienie
orkiestracji interakcji w `BattleInputController`, ale przekazywanie do
`BoardPresenter` wyłącznie:

- współrzędnej;
- gotowego `PreparationHexVisualState`;
- polecenia resetu.

Przenieść z `BoardPresenter` do logiki interakcji:

- iterowanie po wszystkich wyświetlanych polach dla wybranej karty lub jednostki
  i oznaczanie jako legalnych wyłącznie pól dopuszczonych przez reguły gracza;
- wywołania `UnitPlayService.ValidatePlay`;
- wywołania `SpellPlayService.ValidatePlay`;
- wyszukiwanie legalnych celów friendly-unit;
- decyzję `Legal`, `Blocked`, `Selected` lub `Default`.

Jeżeli po migracji te operacje nadmiernie powiększą `BattleInputController`,
wydzielić zwykłą klasę `PreparationHexStateResolver`. Powinna ona wypełniać
przekazany przez wywołującego, prealokowany bufor stanów, bez LINQ i bez
tworzenia kolekcji przy każdym drag ticku. Nie tworzyć jej z góry, jeśli proste
prywatne metody inputu pozostaną czytelne.

Walidacja po stronie `UnitPlayService`, `SpellPlayService` i
`FormationService` pozostaje źródłem prawdy. Kolor hexa nigdy nie zastępuje
walidacji wykonywanej przy zatwierdzeniu akcji.

## 8. Widoczność zależna od fazy

Dodać jedno centralne odświeżenie prezentacji planszy, wywoływane razem z
istniejącym `StateChanged`:

```text
visible = state != null && state.Phase == BattlePhase.Preparation
boardPresenter.SetPreparationHexesVisible(visible)
```

Nie wykonywać tego sprawdzenia w `Update`.

Przejścia powinny dawać następujący rezultat:

| Faza | Widoki pól gracza | Mapowanie pozycji całej planszy |
|---|---:|---:|
| `RoundStart` | ukryte | aktywne |
| `Preparation` / aktywny gracz | widoczne | aktywne |
| `Preparation` / aktywne AI | widoczne | aktywne |
| `Combat` | ukryte | aktywne |
| `RoundResolution` | ukryte | aktywne |
| `MatchEnd` | ukryte | aktywne |

Przy opuszczeniu `Preparation` najpierw anulować drag/selection przez istniejący
`BattleInputController.HandleBattleStateChanged`, wyczyścić stany wizualne, a
następnie ukryć root. Należy zweryfikować kolejność subskrybentów eventu; jeśli
nie jest deterministyczna, cleanup widoku powinien być idempotentny i działać
również po ukryciu rootu.

## 9. Input i raycast

- Zachować `BoxCollider` na każdym widocznym polu gracza.
- Nie używać colliderów niewidocznych pól przeciwnika, ponieważ te widoki nie
  będą tworzone.
- Poza przygotowaniem wspólny nieaktywny root wyłącza collidery bez dodatkowej
  pętli.
- `RaycastForTile` i `TryGetFormationTarget` nadal mogą zwracać
  `HexTileView`, ale tylko jako nośnik `Coord`; nie mogą traktować jego koloru
  jako wyniku walidacji.
- Raycast jednostki podczas przesuwania na zajęte pole gracza nadal mapuje
  `FormationCoord` przez `TryGetTile`.
- `TryGetBoardWorldPosition` podczas dragowania korzysta z płaszczyzny
  `BoardPresenter`, a nie z renderera hexa, więc pozostaje niezależne od
  widoczności sprite'ów.

W osobnym, opcjonalnym kroku można przenieść board collidery na dedykowaną
warstwę i zawęzić `boardRaycastMask`, ale nie jest to wymagane do tego refaktoru
i nie powinno być łączone bez pomiaru kosztu oraz sprawdzenia sceny.

## 10. Migracja prefabu i sceny

Zmiany assetów wykonać przez Unity MCP, ponieważ `PF_HexTile.prefab` ma obecnie
lokalne zmiany i trzeba zachować jego GUID oraz świadomie rozwiązać istniejący
stan.

### `PF_HexTile`

- przypisać istniejący `SpriteRenderer` do `HexTileView`;
- zachować sprite `Hex.png` i prosty materiał sprite bez nowego shadera;
- dostroić `default`, `legal`, `blocked` i `selected` z uwzględnieniem alpha;
- zachować lekki `BoxCollider` dla inputu;
- po potwierdzeniu braku innych zależności usunąć z prefabu nieużywane elementy
  starego mesha: `MeshFilter`, `MeshRenderer`, `MeshCollider` i wyłączone dziecko
  modelu;
- nie zmieniać ani nie odtwarzać pliku `.meta`.

### Scena `Battle`

- dodać lub podłączyć `PreparationHexGridView` i jego root;
- zachować transform przestrzeni planszy używany przez jednostki i pociski;
- zweryfikować, że root sprite'ów można wyłączać bez wyłączania
  `BoardPresenter`;
- zapisać scenę przez Unity i sprawdzić brak missing references.

`StandaloneBattleBootstrap` powinien nadal otrzymać działające mapowanie
pozycji, ale nie powinien automatycznie pokazywać hexów w trybie samej walki.

## 11. Etapy implementacji

### Etap 1 — zabezpieczenie kontraktu testami

- dodać testy liczby i pełnego zakresu tworzonych widoków planszy;
- dodać testy widoczności dla wszystkich faz;
- dodać test potwierdzający, że mapowanie pozycji nie zależy od aktywności
  `HexTileView` ani rootu siatki;
- zachować testy bieżącej geometrii jako zabezpieczenie przed przesunięciem
  jednostek po refaktorze.

Kryterium: czerwone testy opisują docelową separację bez zmiany reguł gameplay.

### Etap 2 — wydzielenie geometrii

- dodać `HexBoardLayout`;
- przenieść `ToLocalPosition` i obliczenie środka;
- przełączyć `BoardPresenter.GetWorldPosition/GetWorldCenter` na layout;
- przenieść testy geometrii z `HexBoardTests` do `HexBoardLayoutTests`;
- usunąć zależność `HexBoard` od Unity, jeśli jest już zbędna.

Kryterium: symulacja i wszystkie prezentery otrzymują identyczne pozycje jak
przed zmianą, niezależnie od istnienia sprite'ów.

### Etap 3 — czysty `HexTileView` na `SpriteRenderer`

- dodać enum stanu wizualnego;
- zamienić `MeshRenderer` i property block na `SpriteRenderer.color`;
- usunąć zależność widoku od `BattleSide` i kolorów stref;
- dodać testy mapowania stanów na kolory.

Kryterium: każda zmiana koloru hexa przechodzi przez `SpriteRenderer`; kod nie
odwołuje się do `_BaseColor` ani `MeshRenderer`.

### Etap 4 — osobny grid pól przygotowania

- wydzielić `PreparationHexGridView`;
- instancjonować wszystkie pola planszy;
- zachować reuse przy tej samej topologii;
- resetować stany i przełączać wspólny root zamiast odbudowywać obiekty.

Kryterium: dla planszy `5 x 8` istnieje dokładnie 40 widoków, lookup działa dla
obu stron, a mapowanie świata pozostaje dostępne także po ukryciu całej siatki.

### Etap 5 — wyniesienie decyzji gameplay z prezentera

- usunąć z `BoardPresenter` metody znające kartę, gracza lub jednostkę;
- przenieść wyznaczanie stanów do `BattleInputController` albo małego,
  bezstanowego `PreparationHexStateResolver`;
- przekazywać widokowi wyłącznie coord i stan wizualny;
- zachować walidację akcji w istniejących serwisach.

Kryterium: pliki prezentacji planszy nie wywołują `UnitPlayService`,
`SpellPlayService` ani `FormationService`.

### Etap 6 — lifecycle fazy i input

- centralnie synchronizować widoczność przy `StateChanged`;
- czyścić selection, hover i drag na wyjściu z przygotowania;
- zweryfikować oba warianty kolejności tury: gracz pierwszy i AI pierwsze;
- upewnić się, że collidery są nieaktywne poza przygotowaniem.

Kryterium: wszystkie hexy pojawiają się i znikają bez `Instantiate`, `Destroy` i
pollingu per-frame.

### Etap 7 — migracja assetów i weryfikacja Unity

- zmigrować `PF_HexTile` z zachowaniem istniejących lokalnych zmian;
- podłączyć referencje w scenie `Battle`;
- sprawdzić kompilację, missing scripts i missing references przez Unity MCP;
- uruchomić wąskie testy Edit Mode, a następnie potrzebne testy Play Mode;
- wykonać kontrolę wizualną w Game View dla typowych proporcji telefonu.

Kryterium: prefab wykorzystuje tylko sprite'ową prezentację, scena serializuje
poprawne referencje, a testy i kompilacja przechodzą.

## 12. Strategia testów

### Edit Mode — logika i geometria

- istniejące testy `HexBoard` dla stref, dystansu i pathfindingu nadal
  przechodzą;
- `HexBoardLayout` zachowuje centrowanie, symetrię rzędów i odległość sąsiadów;
- layout zwraca poprawną pozycję pola każdej strony również przy nieaktywnym
  rootcie widoków;
- zmiana `HexSize` przebudowuje layout i widoki przy zachowaniu skali.

### Edit Mode — prezentacja

- `HexTileView.Initialize` zapisuje coord i ustawia `Default`;
- każdy stan ustawia oczekiwane `SpriteRenderer.color`;
- brak `SpriteRenderer` nie powoduje wyjątku, lecz daje czytelną diagnostykę;
- build tworzy wszystkie pola planszy;
- ten sam topology/layout reuse'uje instancje;
- zmiana rozmiaru planszy lub hexa przebudowuje oczekiwaną liczbę widoków;
- `SetVisible(false)` wyłącza root i collidery;
- ponowne pokazanie rozpoczyna od czystych stanów.

### Edit Mode — interakcje

- wybrana karta jednostki zaznacza tylko legalne pola gracza;
- hover legalny, zablokowany i wybrane pole mapują się na właściwe stany;
- friendly-unit spell podświetla wyłącznie pola poprawnych celów;
- potwierdzenie akcji ponownie przechodzi przez właściwy serwis gameplay;
- zmiana fazy czyści drag i zaznaczenie.

### Play Mode / integracja

- w `RoundStart` hexy są ukryte;
- wejście do `Preparation` pokazuje wszystkie pola planszy;
- wszystkie pola pozostają widoczne podczas tury przygotowania AI;
- wejście do `Combat` natychmiast ukrywa hexy i ich hitboxy;
- jednostki, ruch, pociski, VFX i damage text trafiają w te same pozycje co
  wcześniej;
- po kilku rundach liczba instancji hexów nie rośnie i nie ma starych
  highlightów;
- background/resume nie zmienia widoczności niezgodnie z aktualną fazą.

Testy Unity uruchamiać przez Unity MCP w otwartym Editorze. Nie uruchamiać testów
Edit Mode w batchmode.

## 13. Wydajność mobilna

- Produkcyjna plansza `5 x 8` utrzymuje 40 lekkich sprite rendererów i
  colliderów, ale są one aktywne tylko podczas przygotowania.
- Poza przygotowaniem wspólny nieaktywny root eliminuje renderowanie, overdraw i
  udział colliderów hexów w raycastach.
- Widoki są budowane raz na topology i nie powodują churnu pamięci między
  rundami.
- `SpriteRenderer.color` nie tworzy instancji materiałów i nie wymaga
  `MaterialPropertyBlock`.
- Nie dodawać `Update`, coroutine ani pollingu fazy do widoków.
- Iterowanie po polach podczas zmiany selekcji jest dopuszczalne; podczas drag
  aktualizować tylko poprzedni i bieżący hover, zamiast przepisywać kolory całej
  siatki co klatkę.
- Nie dodawać nowego materiału, shadera, tekstury ani pakietu.

Punkty profilowania po implementacji:

- liczba aktywnych `SpriteRenderer` i colliderów w każdej fazie;
- GC Alloc przy wejściu/wyjściu z `Preparation`;
- GC Alloc podczas dragowania karty i jednostki;
- koszt raycastu inputu na urządzeniu mobilnym;
- transparent overdraw sprite'ów hexów w maksymalnie podświetlonym układzie.

## 14. Ryzyka i zabezpieczenia

### Wyłączenie całego `BoardPresenter`

Ryzyko: jednostki i pociski stracą mapowanie pozycji w walce.

Zabezpieczenie: wyłączany jest wyłącznie root `PreparationHexGridView`;
`HexBoardLayout` i `BoardPresenter` pozostają aktywne.

### Zależność pozycji świata od aktywnego widoku

Ryzyko: call-site użyje aktywnego `HexTileView` do pozycjonowania walki i
przestanie działać po ukryciu rootu.

Zabezpieczenie: wszystkie pozycje świata muszą przejść przez
`GetWorldPosition`; test i wyszukanie call-site'ów chronią przed zależnością od
aktywności widoku pola.

### Nieaktualny highlight po rundzie

Ryzyko: wyłączenie rootu zachowa kolor i pokaże go w następnej rundzie.

Zabezpieczenie: `ResetAllStates` przy ukryciu oraz przy rozpoczęciu nowej
interakcji; operacja idempotentna.

### Dublowanie walidacji

Ryzyko: kolor uzna pole za legalne, ale akcja użyje innej reguły.

Zabezpieczenie: resolver highlightu używa tych samych publicznych validatorów,
a controller ponownie waliduje akcję przy jej wykonaniu.

### Lokalne zmiany prefabu

Ryzyko: migracja nadpisze niezacommitowaną pracę w `PF_HexTile`.

Zabezpieczenie: przed edycją porównać prefab z `HEAD`, zachować bieżący sprite,
transform i collider, a zmianę wykonać i zapisać przez Unity MCP bez ruszania
GUID ani `.meta`.

### Zmiana geometrii przez przeniesienie `ToLocalPosition`

Ryzyko: minimalna różnica wzoru przesunie jednostki lub złamie facing.

Zabezpieczenie: przenieść wzór bez modyfikacji i zachować testy dokładnych
pozycji, symetrii oraz `BattleViewFacingTests`.

## 15. Główne pliki objęte zmianą

### Logika i geometria

- `Assets/DeckBattle/Scripts/Board/HexBoard.cs`
- `Assets/DeckBattle/Scripts/Board/HexBoardLayout.cs` — nowy
- `Assets/DeckBattle/Scripts/Board/BoardPresenter.cs`

### Prezentacja i input

- `Assets/DeckBattle/Scripts/Board/PreparationHexVisualState.cs` — nowy
- `Assets/DeckBattle/Scripts/Board/PreparationHexGridView.cs` — nowy
- `Assets/DeckBattle/Scripts/Board/HexTileView.cs`
- `Assets/DeckBattle/Scripts/Input/BattleInputController.cs`
- `Assets/DeckBattle/Scripts/Battle/BattleController.cs`

### Assety

- `Assets/DeckBattle/Prefabs/Battle/PF_HexTile.prefab`
- `Assets/DeckBattle/Scenes/Battle.unity`

### Testy

- `Assets/DeckBattle/Tests/EditMode/HexBoardTests.cs`
- `Assets/DeckBattle/Tests/EditMode/HexBoardLayoutTests.cs` — nowy
- `Assets/DeckBattle/Tests/EditMode/BoardPresenterTests.cs`
- `Assets/DeckBattle/Tests/EditMode/HexTileViewTests.cs` — nowy
- test integracyjny lifecycle widoczności, jeżeli obecny harness controllera nie
  pokrywa przejść faz.

## 16. Zakres poza refaktorem

- zmiana zasad deploymentu i liczby rzędów po stronie gracza;
- zmiana algorytmów pathfindingu, targetowania lub walki;
- osobne style lub reguły kolorowania stref gracza i przeciwnika;
- podgląd zasięgu jednostek podczas `Combat`;
- przebudowa inputu z raycastów fizycznych na matematyczne wyliczanie coord;
- nowy shader, Shader Graph, VFX lub animacja hexów;
- zmiana `BattlePhase` i kolejności tur przygotowania.

## 17. Definition of Done

Refaktor jest zakończony, gdy:

- `HexBoard` nie zawiera kodu renderowania ani mapowania do `Vector3`;
- pozycja świata każdego pola jest dostępna niezależnie od widoczności siatki;
- `BoardPresenter` nie wywołuje serwisów walidacji gameplay;
- `HexTileView` zna tylko coord, stan wizualny, kolory i `SpriteRenderer`;
- wszystkie kolory są ustawiane przez `SpriteRenderer.color`;
- prefab nie wymaga `MeshRenderer` ani `MaterialPropertyBlock`;
- istnieje dokładnie jeden widok dla każdego pola planszy;
- widoki są aktywne tylko w zatwierdzonym zakresie fazy przygotowania;
- poza przygotowaniem sprite'y i collidery hexów są nieaktywne;
- przejścia faz nie tworzą i nie niszczą instancji hexów;
- input kart, zaklęć i formacji zachowuje dotychczasowe reguły;
- jednostki, pociski i efekty zachowują dotychczasowe pozycje podczas walki;
- testy Edit Mode i wymagane testy Play Mode przechodzą;
- Unity nie raportuje błędów kompilacji, missing scripts ani missing references;
- profiler nie pokazuje nowych alokacji per-frame ani dodatkowego overdraw poza
  fazą przygotowania.
