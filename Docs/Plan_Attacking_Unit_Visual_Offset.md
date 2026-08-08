# Plan: wizualny offset jednostek atakujących

## 1. Cel

Dodać lekkie, wyłącznie prezentacyjne dosunięcie jednostki w stronę aktualnie
atakowanego celu. Domyślna wartość przesunięcia ma wynosić:

```csharp
0.15f
```

Efekt ma zmniejszyć wizualny odstęp między walczącymi jednostkami bez zmiany:

- zajmowanego heksa;
- zasięgu ataku;
- targetowania i pathfindingu;
- kolizji oraz rezerwacji pól;
- deterministycznego stanu symulacji.

Jeżeli dwie jednostki atakują siebie nawzajem, każda przesuwa się o własne
`0.15f`, więc ich wizualny dystans zmniejsza się łącznie o `0.30f`.

## 2. Przyjęta semantyka

Offset jest aktywny tak długo, jak `UnitView` zna aktualny cel przekazany przez
`UnitTargetChanged` lub event fazy ataku. Nie jest osobnym lungem uruchamianym
przy każdym ciosie. Zapobiega to powtarzanemu przesuwaniu jednostki do przodu i
do tyłu podczas kolejnych windupów i cooldownów.

Przejście do offsetu, zmiana kierunku oraz powrót po utracie celu są łagodzone
lekkim lerpem. Offset pozostaje aktywny przez cały normalny atak i targetowany
special, zamiast resetować się między windupem, fire i końcem pojedynczej
animacji.

Przesunięcie:

- działa tylko w płaszczyźnie planszy (`XZ`);
- jest liczone od środka logicznego heksa w kierunku środka heksa celu;
- nie zmienia pionowego `groundOffset` jednostki;
- nie kumuluje się po kolejnych eventach celu;
- znika po utracie celu, ponownym `Bind`, wyczyszczeniu bitwy lub zwolnieniu
  widoku;
- dla wartości `0f` zachowuje obecne pozycjonowanie jeden do jednego.

Offset obejmuje zarówno jednostki walczące wręcz, jak i dystansowe. Jest to
celowe: parametr opisuje korektę prezentacji jednostki posiadającej cel, a nie
regułę zasięgu konkretnego typu ataku.

## 3. Stan obecny

- `BattleTickLoop` emituje `UnitTargetChanged` przy wyborze celu, zmianie jego
  heksa oraz utracie celu.
- `BattleUnitPresenter` mapuje heks celu na world position i przekazuje go do
  `UnitView.SetTargetWorldPosition`.
- `BattleUnitPresenter.HandleMoved` przekazuje środek docelowego heksa do
  `UnitView.MoveToWorldPosition`.
- `UnitView` interpoluje bezpośrednio `transform.position` i przechowuje w
  kolejce gotowe pozycje świata zawierające `groundOffset`.
- `UnitView` zna ostatnią pozycję celu wyłącznie na potrzeby kierunku patrzenia.
- Symulacja oraz `BattleEvent` operują na `HexCoord`, dzięki czemu korektę można
  dodać bez rozszerzania kontraktu gameplayowego.

Najważniejsze ryzyko obecnego modelu to dryf: dodawanie `0.15f` bezpośrednio do
bieżącego `transform.position` kumulowałoby przesunięcie przy każdym odświeżeniu
celu. Dlatego widok musi rozróżniać bazową pozycję planszy i wynikową pozycję
prezentacyjną.

## 4. Konfiguracja

W `BattleView` dodać jedno pole konfiguracyjne dla całej prezentacji bitwy:

```csharp
[SerializeField, Min(0f)]
private float attackingUnitOffset = 0.15f;

[SerializeField, Min(0.01f)]
private float attackingUnitOffsetLerpSpeed = 12f;
```

Wartość należy zabezpieczyć przez `Mathf.Max(0f, attackingUnitOffset)` przed
przekazaniem jej dalej. Jedno pole w `BattleView` jest preferowane względem pola
na każdym prefabie `UnitView`, ponieważ:

- wszystkie jednostki otrzymują ten sam domyślny tuning;
- nie trzeba aktualizować wielu wariantów prefabów;
- zmiana balansu wizualnego jest wykonywana w jednym miejscu;
- parametr nie trafia do `BattleRuntimeTuning`, bo nie wpływa na symulację.

`BattleView.EnsurePresenters` przekazuje bezpieczną wartość do
`BattleUnitPresenter`, a presenter ustawia ją na każdym widoku podczas
`BindInitial`. Wartość musi działać także dla widoków przejętych z fazy
przygotowania, nie tylko dla nowo utworzonych prefabów.

## 5. Model pozycji w `UnitView`

### 5.1. Pozycja bazowa i prezentacyjna

Rozdzielić pozycję na dwa pojęcia:

```text
baseBoardWorldPosition
  = pozycja wynikająca wyłącznie z heksa

presentedWorldPosition
  = baseBoardWorldPosition
  + poziomy offset w stronę celu
  + Vector3.up * groundOffset
```

Nie zmieniać `modelRoot.localPosition`, ponieważ Animator może sterować
transformami modelu, a overlaye i pivoty statusów powinny nadal podążać za
całym `UnitView`.

### 5.2. Jedno obliczenie pozycji

Dodać mały helper używany przez wszystkie ścieżki ustawiania pozycji:

```text
ResolvePresentedPosition(basePosition, targetPosition, hasTarget, offset)
```

Helper powinien:

1. wyzerować składową `Y` kierunku do celu;
2. użyć `sqrMagnitude`, aby uniknąć zbędnego pierwiastka przy zerowym kierunku;
3. dla poprawnego kierunku dodać `normalizedDirection * offset`;
4. na końcu dodać istniejący `groundOffset`;
5. dla braku celu, zerowego offsetu lub praktycznie zerowego kierunku zwrócić
   nieprzesuniętą pozycję bazową.

Nie dodawać offsetu do już przesuniętego `transform.position`.

### 5.3. Ruch i kolejka waypointów

`SetWorldPosition` i `MoveToWorldPosition` powinny przyjmować nadal bazową
pozycję z `BoardPresenter`, ale wewnętrznie przechowywać waypointy bez
`groundOffset` i bez offsetu ataku.

Podczas aktywnego segmentu ruchu:

- interpolować pozycję bazową;
- obliczyć wynikową pozycję prezentacyjną na podstawie aktualnego celu;
- zapisać `transform.position` tylko raz na krok animacji.

Stałe tablice kolejki pozostają reużywane. Nie dodawać list, LINQ, coroutine ani
alokacji per ruch. Zmiana celu podczas oczekujących waypointów nie może zostawić
w kolejce starego, wcześniej doliczonego offsetu.

`UnitView` przechowuje bieżący i docelowy poziomy offset. W istniejącym,
warunkowo aktywnym `Update` zbliża bieżącą wartość do docelowej przez
`Vector3.Lerp` z zależnym od czasu współczynnikiem. Widok aktualizuje się tylko
podczas ruchu, obrotu, efektu obrażeń/śmierci albo trwającego lerpu offsetu.

### 5.4. Zmiana i utrata celu

`SetTargetWorldPosition` powinno:

- zapisać nową bazową pozycję celu;
- ustawić flagę aktywnego celu;
- przeliczyć bieżącą pozycję prezentacyjną bez kumulowania przesunięcia;
- zachować istniejące reguły obracania po zakończeniu kolejki ruchu.

`ClearTargetWorldPosition` powinno wyzerować flagę i przywrócić pozycję nad
środkiem aktualnego bazowego heksa. Korekta `0.15f` może zostać zastosowana lub
usunięta natychmiast — nie należy uruchamiać dla niej animacji biegu ani nowej
sekwencji tween.

`ResetTransientState` musi wyczyścić cel i offset przed ustawieniem pozycji, aby
ponownie użyty widok nie odziedziczył stanu poprzedniej jednostki.

## 6. Integracja prezentera

### `BattleView`

- dodać serializowany `attackingUnitOffset = 0.15f`;
- przekazać go przy tworzeniu `BattleUnitPresenter`;
- nie dodawać pola do `BattleEvent`, snapshotu ani konfiguracji symulacji.

### `BattleUnitPresenter`

- przechować bezpieczną, nieujemną wartość offsetu;
- ustawić ją na `UnitView` podczas `BindInitial`;
- pozostawić `HandleMoved` oparty na `battleEvent.To` — dopiero `UnitView`
  nakłada warstwę prezentacyjną;
- nadal aktualizować pozycję celu w `HandleTargetChanged`, windupie, fire oraz
  targetowanych specialach;
- przy `TargetUnitId <= 0` wywołać istniejące czyszczenie celu, które odtworzy
  pozycję bez offsetu.

Nie zmieniać `AttackPositionSelector`, `MovementResolver`, `TargetSelector`,
`HexBoard` ani `UnitRuntimeState`.

## 7. Spójność efektów

Po przesunięciu roota automatycznie podążą za nim:

- status overlay;
- `StatusVfxPivot`;
- floating damage text pobierający `view.transform.position`;
- cel aktywnego pocisku, ponieważ `ProjectileView` śledzi transform celu.

Podczas weryfikacji sprawdzić źródło pocisku i attack/damage VFX, które obecnie
mogą startować ze środka heksa. Jeżeli różnica `0.15f` jest widoczna, pobrać
`X/Z` z aktualnego `UnitView`, pozostawiając dotychczasową wysokość wyliczoną z
`BoardPresenter` i konfiguracji efektu. Nie wolno przez tę korektę podwójnie
dodać `groundOffset` ani zmienić logiki trafienia pocisku.

## 8. Testy

### `UnitViewFacingTests`

Dodać testy potwierdzające, że:

1. cel na osi `+X` przesuwa widok dokładnie o domyślne `0.15f`;
2. kierunek diagonalny zachowuje długość offsetu `0.15f`;
3. składowa pionowa celu nie zmienia poziomego kierunku ani `groundOffset`;
4. wielokrotne ustawienie tego samego celu nie kumuluje przesunięcia;
5. zmiana celu przelicza offset od bazowej pozycji, a nie od poprzedniego
   wyniku;
6. wyczyszczenie celu przywraca środek bazowego heksa;
7. `offset = 0f` zachowuje obecne pozycjonowanie;
8. cel w tej samej pozycji nie produkuje `NaN`;
9. ruch i kolejka waypointów kończą się na ostatnim heksie z aktualnym, a nie
   historycznym offsetem.

### `BattleViewFacingTests`

Dodać test integracyjny eventów:

```text
UnitTargetChanged -> UnitMoved -> zakończenie ruchu
```

Końcowy `UnitView.transform.position` ma odpowiadać środkowi docelowego heksa
przesuniętemu o `0.15f` w stronę celu. Osobny test
`UnitTargetChanged(... NoTarget ...)` ma potwierdzić powrót na środek heksa.

Istniejące testy kierunku patrzenia i interpolacji kolejki muszą pozostać
zielone po rozdzieleniu pozycji bazowej od prezentacyjnej.

## 9. Weryfikacja w Unity

Po implementacji:

1. użyć Unity MCP do sprawdzenia kompilacji;
2. uruchomić najpierw `UnitViewFacingTests` i `BattleViewFacingTests` w Edit Mode;
3. uruchomić scenę bitwy i sprawdzić pary: melee–melee, melee–ranged oraz
   ranged–ranged;
4. sprawdzić zmianę celu, śmierć celu, utratę targetu i kolejną rundę;
5. potwierdzić, że jednostki stoją logicznie na poprawnych heksach mimo korekty
   modelu;
6. sprawdzić pociski, attack VFX, damage VFX, paski HP i status VFX;
7. w Profilerze potwierdzić `0 B GC Alloc` dla odświeżenia celu i aktywnego
   ruchu po rozgrzaniu.

Nie uruchamiać testów Edit Mode przez Unity batchmode.

## 10. Wpływ na mobile i URP

- brak nowych `GameObject`, materiałów, shaderów i wariantów URP;
- brak nowych `Update` lub coroutine;
- brak alokacji zarządzanych per tick i per atak;
- dodatkowy koszt ogranicza się do kilku operacji `Vector3` podczas eventu celu
  oraz istniejącej aktualizacji aktywnego ruchu;
- brak wpływu na tekstury, overdraw, build size i stan symulacji.

## 11. Pliki objęte wdrożeniem

Planowane zmiany:

- `Assets/DeckBattle/Scripts/Battle/BattleView.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleUnitPresenter.cs`;
- `Assets/DeckBattle/Scripts/Battle/UnitView.cs`;
- `Assets/DeckBattle/Tests/EditMode/UnitViewFacingTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/BattleViewFacingTests.cs`.

Opcjonalnie, tylko jeżeli test wizualny ujawni rozjazd punktów startowych:

- `Assets/DeckBattle/Scripts/Battle/BattleProjectilePresenter.cs`;
- obsługa attack/damage VFX w `BattleView` lub `BattleUnitPresenter`.

## 12. Kryteria akceptacji

- domyślna wartość offsetu wynosi `0.15f`;
- aktywna jednostka jest przesunięta w stronę aktualnego celu dokładnie raz;
- dwie wzajemnie atakujące jednostki są wizualnie bliżej o `0.30f`;
- utrata celu przywraca widok nad środek logicznego heksa;
- offset nie wpływa na gameplay, deterministykę ani eventy symulacji;
- ruch po wielu waypointach nie dryfuje i używa aktualnego celu;
- brak nowych alokacji w gorącej ścieżce i brak dodatkowego stałego tickowania;
- wszystkie zawężone testy Edit Mode przechodzą;
- na scenie bitwy nie ma widocznego rozjazdu jednostek, overlayów, VFX ani
  pocisków.
