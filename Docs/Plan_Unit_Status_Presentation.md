# Plan prezentacji statusów na jednostkach

## 1. Cel

Rozbudować istniejącą prezentację statusów tak, aby każdy `StatusKind` mógł
korzystać z dokładnie jednego kanału:

- `Icon` — pojedyncza ikona w `UnitStatusOverlay`;
- `Vfx` — efekty przypięte do pivota `UnitView`;
- `None` — brak prezentacji, dopóki status nie zostanie skonfigurowany.

Podział konkretnych statusów pomiędzy `Icon` i `Vfx` zostanie wykonany osobno.
Implementacja ma dostarczyć dane i narzędzia do tego podziału, ale nie powinna
samodzielnie aktywować nowych prezentacji w produkcyjnym contencie.

## 2. Zatwierdzone założenia

1. Status nie może jednocześnie używać ikony i VFX.
2. Status VFX może mieć trzy niezależne fazy:
   - jednorazowy efekt nałożenia/dodania stacka;
   - efekt aktywny przez czas trwania statusu;
   - jednorazowy efekt zdjęcia stacka/statusu.
3. Wszystkie VFX są przypinane do jednego punktu: pivota głównego
   `Transform` komponentu `UnitView`.
4. Ikona nie pokazuje czasu, liczby stacków ani wartości statusu.
5. Dla statusu VFX liczba aktywnych efektów ciągłych musi być równa sumie jego
   aktywnych stacków na jednostce. Każdy nowy stack otrzymuje osobną instancję
   tego samego VFX.
6. Konfiguracja ma być data-driven i nie może wymagać dopisywania kolejnego
   `switcha` w widoku przy każdym nowym statusie.

## 3. Stan obecny

- `StatusDefinition` zawiera pola `Icon` i `DisplayColor`, ale ikony w
  produkcyjnych assetach statusów nie są przypisane.
- `UnitStatusOverlayView` tworzy do czterech kolorowych slotów ze skrótami
  literowymi. Nie korzysta z `StatusDefinition.Icon`.
- Overlay jest poolowany i aktualizowany po eventach statusów, co należy
  zachować.
- `BattleView` używa jednego wspólnego `statusEffectPrefab` wyłącznie przy
  nałożeniu `Stun`, `Shield`, `Invulnerability` i `Mark`.
- Obecny `PooledBattleEffect` obsługuje krótki efekt w pozycji świata, ale nie
  obsługuje efektu ciągłego przypiętego do poruszającej się jednostki.
- Eventy statusów przenoszą `StatusKind`, source ID i liczbę stacków, więc
  warstwa prezentacji nie musi otrzymywać referencji do gameplayowych
  `ScriptableObject`.

## 4. Model danych prezentacji

### 4.1. Osobny katalog prezentacji

Dodać `StatusPresentationCatalog : ScriptableObject`, niezależny od logiki
symulacji. Katalog zawiera tablicę wpisów indeksowanych przez `StatusKind`.

Proponowany wpis:

```text
StatusKind Kind
StatusPresentationMode Mode       // None, Icon, Vfx
int Priority

Sprite Icon                       // tylko Mode.Icon

StatusVfxView ApplyVfxPrefab      // tylko Mode.Vfx, opcjonalny
StatusVfxView ActiveVfxPrefab     // tylko Mode.Vfx, opcjonalny
StatusVfxView RemoveVfxPrefab     // tylko Mode.Vfx, opcjonalny

Vector3 LocalPosition
Vector3 LocalEulerAngles
Vector3 LocalScale

int PrewarmCountPerPrefab
```

`StatusPresentationCatalog` powinien po inicjalizacji zbudować stałą tablicę
lookup według wartości `StatusKind`. Odczyt w walce ma być O(1), bez LINQ,
alokacji i przeszukiwania listy.

### 4.2. Walidacja authoringu

Walidacja Editora powinna wykrywać:

- zduplikowany `StatusKind`;
- `StatusKind.None` we wpisie;
- ikonę lub prefab VFX niezgodny z wybranym `Mode`;
- `Icon` bez przypisanego sprite'a;
- `Vfx` bez żadnego z trzech prefabów;
- niepoprawną skalę lub liczbę prewarm;
- status VFX o `MaxStacks` większym niż zatwierdzony budżet prezentacji.

Tryb `None` jest poprawny i będzie domyślny, dopóki podział statusów nie
zostanie ustalony.

Nie rozszerzać dalej gameplayowego `StatusDefinition` o pola VFX. Istniejące
`Icon` i `DisplayColor` pozostawić tymczasowo dla zgodności assetów, oznaczyć
jako legacy i usunąć dopiero w osobnej migracji po podłączeniu katalogu.

## 5. Prezentacja ikon

Zmodyfikować `UnitStatusOverlayView.SetStatuses`, aby korzystał z katalogu:

1. przeskanować `UnitStatusCollection`;
2. pominąć wpisy w trybie `None` i `Vfx`;
3. zgrupować powtarzające się instancje tego samego `StatusKind` do jednej
   ikony;
4. uporządkować ikony według `Priority`, a potem stabilnie według
   `StatusKind`;
5. przypisać sprite do istniejących, poolowanych slotów;
6. nie tworzyć ani nie aktualizować tekstu dla statusu.

Zachować limit czterech slotów na telefonie. Jeśli statusów ikonowych jest
więcej, ostatni slot może nadal być technicznym `+N`; nie jest to informacja
o stackach pojedynczego statusu. Sloty najlepiej umieścić na stałe w
`PF_UnitStatusOverlay` i zserializować ich referencje zamiast tworzyć
GameObjecty przy pierwszym statusie.

Overlay nadal jest aktualizowany tylko po:

- `StatusApplied`;
- `StatusRefreshed`;
- `StatusStackChanged`;
- `StatusRemoved`;
- początkowym związaniu widoku z jednostką.

Nie dodawać odliczania czasu ani odświeżania layoutu w `Update`.

## 6. Prezentacja VFX

### 6.1. Widok pojedynczego efektu

Dodać lekki `StatusVfxView`, który obsługuje dwa sposoby działania:

- `PlayOneShot(Transform pivot)` — efekt nałożenia lub zdjęcia, po zakończeniu
  sam zgłasza gotowość do zwrotu do puli;
- `BeginActive(Transform pivot)` — efekt ciągły, parentowany do pivota;
- `Release()` — zatrzymanie Particle System/VFX i pełny reset przed poolingiem.

Prefab może używać lekkiego `ParticleSystem`, animowanego mesha albo innej
istniejącej techniki URP. Interfejs puli nie powinien zależeć od konkretnego
rodzaju renderera.

Po pobraniu z puli efekt otrzymuje:

- `SetParent(unitView.transform, false)`;
- lokalną pozycję, obrót i skalę z katalogu;
- wyzerowany stan poprzedniego użycia.

### 6.2. Centralny kontroler i pooling

Dodać `UnitStatusVfxController` po stronie prezentacji walki. Kontroler:

- przechowuje aktywne VFX według `unitId` i `StatusKind`;
- posiada osobną pulę dla każdego prefabu;
- prewarmuje pule przed rozpoczęciem walki;
- nie wykonuje `Instantiate`/`Destroy` w ustabilizowanej walce;
- nie skanuje jednostek w `Update`;
- aktualizuje się wyłącznie po eventach i przy bind/rebuild widoku.

Efekt ciągły jest dzieckiem jednostki, dlatego nie wymaga ręcznego
przeliczania pozycji co klatkę.

### 6.3. Semantyka stacków

Dla każdego statusu VFX kontroler utrzymuje:

```text
desiredActiveCount = suma StatusInstance.Stacks dla danego StatusKind
```

Następnie wykonuje reconciliation:

- jeśli `desiredActiveCount > activeCount`, pobiera różnicę z puli;
- jeśli `desiredActiveCount < activeCount`, zwalnia różnicę do puli;
- jeśli wartości są równe, nie wykonuje pracy.

Przykład: status przechodzi z 2 do 4 stacków — kontroler dodaje dokładnie dwie
instancje aktywnego VFX. Powrót z 4 do 1 zwalnia trzy instancje.

Reguła dotyczy również wielu `StatusInstance` tego samego rodzaju pochodzących
z różnych źródeł: na jednostce liczy się łączna liczba stacków.

### 6.4. Efekty jednorazowe

Warstwa prezentacji utrzymuje mały shadow-state liczby stacków według:

```text
unitId + StatusKind + sourceUnitId
```

Dzięki temu może poprawnie interpretować istniejące eventy:

- `StatusApplied` — odtworzyć Apply VFX dla każdego dodanego stacka;
- `StatusRefreshed`/`StatusStackChanged` — odtworzyć Apply VFX tylko dla
  dodatniej różnicy stacków albo Remove VFX dla różnicy ujemnej;
- refresh bez zmiany stacków nie tworzy nowego efektu;
- `StatusRemoved` — odtworzyć Remove VFX dla każdego usuniętego stacka.

Po przetworzeniu całej paczki eventów wykonać reconciliation z aktualnym
`UnitStatusCollection`. Stan symulacji pozostaje źródłem prawdy i naprawia
ewentualną utratę lub połączenie eventów.

Przy początkowym bindzie lub odbudowie sceny tworzyć tylko aktywne VFX — bez
fałszywego efektu nałożenia. Przy śmierci, końcu walki i `ReleaseAll` zwalniać
efekty bez odtwarzania Remove VFX.

## 7. Integracja z istniejącym przepływem

### `BattleView`

- dodać referencje do `StatusPresentationCatalog` i
  `UnitStatusVfxController`;
- zastąpić `HandleStatusApplied` oparte na jednym `statusEffectPrefab`
  przekazaniem eventu do nowego kontrolera;
- po paczce eventów zsynchronizować ikonę i aktywne VFX z finalnym stanem
  jednostki;
- przy tworzeniu/reużyciu `UnitView` związać pivot z kontrolerem VFX;
- przy `UnitDied`, końcu rundy oraz zwalnianiu widoków zwolnić wszystkie VFX
  jednostki.

### `UnitStatusOverlayController`

- otrzymać katalog prezentacji raz przy inicjalizacji;
- przekazywać go do `UnitStatusOverlayView`;
- zachować obecny pooling, pozycjonowanie i aktualizację eventową.

### `UnitView`

- udostępnić read-only `StatusVfxPivot`, zwracający główny `transform`;
- nie dodawać osobnych punktów `Head/Body/Feet`;
- dopilnować zwolnienia powiązanych VFX przed ponownym bindem lub
  dezaktywacją widoku.

### Assety i scena

- utworzyć jeden produkcyjny `StatusPresentationCatalog`;
- podłączyć go w scenie `Battle`;
- usunąć po migracji użycie wspólnego `PF_StatusApplicationEffect`;
- pozostawić wszystkie wpisy jako `None`, dopóki nie zostanie ustalony
  właściwy podział statusów.

## 8. Budżet mobilny

- VFX przeznaczone dla stackowanych statusów muszą być wyjątkowo lekkie,
  ponieważ liczba rendererów/particle systemów rośnie liniowo ze stackami.
- Maksymalna liczba instancji wynika z `StatusDefinition.MaxStacks`; content
  VFX nie może omijać tego limitu.
- Ustalić budżet authoringu dla `MaxStacks` statusów VFX i walidować go przed
  wejściem do Play Mode/buildem.
- Preferować jeden prosty materiał URP, małe tekstury, brak realtime lights,
  brak cieni i niski transparent overdraw.
- Efekty ciągłe nie powinny mieć własnego skryptu `Update`, jeśli mogą działać
  wyłącznie przez Particle System/Animator.
- Pula powinna być prewarmowana zgodnie z realistycznym maksimum jednostek i
  stacków. Przekroczenie puli w Development Build powinno generować
  diagnostykę z nazwą prefabu i wymaganym rozmiarem.
- Profilować liczbę aktywnych rendererów, particle count, overdraw, czas
  `BattleView` oraz GC Alloc na urządzeniu klasy mid-range Android.

## 9. Testy

### Edit Mode

- katalog zwraca poprawny wpis w O(1);
- walidacja odrzuca duplikaty i jednoczesną konfigurację Icon/VFX;
- status `Icon` tworzy jedną ikonę niezależnie od liczby stacków i źródeł;
- status `Vfx` nie pojawia się w overlay;
- priorytety ikon i `+N` są stabilne;
- reconciliation tworzy i zwalnia dokładną różnicę instancji;
- 1 → 3 stacki dodaje dwa aktywne VFX;
- 3 → 1 stack zwalnia dwa aktywne VFX;
- refresh 3 → 3 nie tworzy Apply VFX;
- wiele źródeł tego samego statusu daje poprawną łączną liczbę VFX;
- `Release` czyści shadow-state i aktywne referencje.

### Play Mode

- aktywny VFX podąża za poruszającą się jednostką przez parenting do pivota;
- Apply, Active i Remove korzystają z właściwych prefabów;
- po wygaśnięciu statusu nie zostaje aktywny VFX;
- po śmierci, ponownym bindzie i następnej rundzie nie zostają stare efekty;
- seria wielu eventów w jednym ticku kończy się prezentacją zgodną z finalnym
  `UnitStatusCollection`;
- pooling nie miesza pozycji, skali, parenta ani stanu particle systemów
  pomiędzy jednostkami.

### Wydajność

- po prewarmie dodawanie/usuwanie statusów nie generuje GC Alloc;
- brak `Instantiate` i `Destroy` podczas ustabilizowanej walki;
- brak per-frame skanowania statusów;
- test stresowy wykorzystuje maksymalną liczbę jednostek i zatwierdzone
  maksymalne stacki statusów VFX.

## 10. Etapy realizacji

### Etap 1 — kontrakt danych

- dodać `StatusPresentationMode`, wpis i katalog;
- dodać walidację oraz testy lookup;
- utworzyć pusty katalog produkcyjny.

### Etap 2 — ikony

- przebudować sloty overlay na prawdziwe sprite'y;
- usunąć skróty i wartości statusów;
- dodać filtrowanie `Icon`/`Vfx`/`None`, deduplikację i priorytety;
- zachować limit czterech slotów i pooling.

### Etap 3 — VFX i pooling

- dodać `StatusVfxView`;
- dodać pule per prefab i prewarm;
- obsłużyć Apply, Active oraz Remove na pivocie jednostki.

### Etap 4 — stacki i lifecycle

- dodać shadow-state per source;
- wdrożyć reconciliation liczby aktywnych VFX;
- podłączyć wszystkie eventy, bind, śmierć i cleanup rundy.

### Etap 5 — testy i profilowanie

- uruchomić testy Edit Mode czystej logiki;
- uruchomić testy Play Mode przez Unity MCP;
- sprawdzić burst eventów, ponowne użycie widoków i brak wycieków puli;
- wykonać profil mobilny oraz ustalić bezpieczny budżet stackowanych VFX.

### Etap 6 — późniejsza konfiguracja contentu

- użytkownik przypisuje każdemu statusowi `None`, `Icon` albo `Vfx`;
- przypisuje sprite lub trzy prefaby VFX;
- dobiera priorytety ikon, parametry transformacji i rozmiary prewarm;
- po zatwierdzeniu contentu usunąć legacy `Icon`/`DisplayColor` oraz stary
  wspólny `statusEffectPrefab`.

## 11. Kryteria akceptacji

- każdy status ma najwyżej jeden kanał prezentacji;
- status ikonowy pokazuje wyłącznie ikonę, bez czasu, stacków i wartości;
- status VFX może obsłużyć Apply, Active i Remove;
- liczba aktywnych VFX statusu jest zawsze równa łącznej liczbie jego stacków;
- VFX porusza się razem z pivotem jednostki;
- UI i VFX odtwarzają finalny stan po dowolnej paczce eventów;
- śmierć, rebind i koniec rundy nie pozostawiają aktywnych efektów;
- po prewarmie system nie alokuje pamięci i nie instancjonuje obiektów w
  ustabilizowanej walce;
- podział konkretnych statusów może zostać wykonany później wyłącznie przez
  konfigurację katalogu.
