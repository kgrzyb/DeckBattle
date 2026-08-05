# Plan: floating damage text przy jednostkach

## 1. Cel

Dodać lekki, czytelny i rozszerzalny system liczb obrażeń wyświetlanych przy
trafionej jednostce. Pierwsza wersja obsługuje dwa warianty prezentacji:

- `Normal` — każde niekrytyczne obrażenie;
- `Critical` — obrażenie krytyczne, wyróżnione kolorem, rozmiarem i krótkim
  akcentem skali.

System ma działać dla ataków wręcz, pocisków, obrażeń okresowych, obrażeń ze
statusów/specjali oraz przekierowania przez Guard. Ma pokazywać wartość
faktycznie zapisaną w `BattleEvent.UnitDamaged`, czyli po modyfikatorach,
tarczach i ewentualnym podziale obrażeń.

## 2. Zakres pierwszej wersji

W zakresie:

- jeden tekst na każdy event `UnitDamaged` o dodatnim `Amount`;
- warianty `Normal` i `Critical`;
- krótki ruch w górę, lekka zmiana skali i wygaszenie alpha;
- poprawne pozycjonowanie na ekranie względem trafionej jednostki;
- pooling, prewarm, limit aktywnych elementów i pełny reset przy ponownym użyciu;
- obsługa prędkości walki 1x/2x;
- czyszczenie przy rebindzie, zmianie rundy i zamknięciu widoku bitwy;
- testy kontraktu eventów, animacji, poolingu i integracji z `BattleView`.

Poza zakresem:

- heal, shield, miss, dodge, immune i nazwy statusów jako floating text;
- łączenie wielu trafień w jedną sumę;
- lokalizacja napisów innych niż liczby;
- osobne fonty, materiały lub prefabrykaty dla każdego typu obrażeń;
- ciężkie animacje Animator/DOTween, particle VFX, shadery i dźwięk krytyka.

## 3. Stan obecny

- `DamageResolver` jest wspólnym miejscem rozliczania obrażeń i emituje
  `BattleEvent.UnitDamaged` dopiero po ekspozycji, tarczach i Guardzie.
- Wartość krytyka znajduje się w `DamageRequest.IsCritical`, ale nie trafia do
  eventu obrażeń.
- Osobny `BattleEventType.UnitCrit` jest emitowany przed `UnitDamaged`. Nie jest
  to bezpieczny kontrakt do parowania tekstów: jeden krytyk może zostać
  rozdzielony na Guard i chroniony cel, a trafienie całkowicie pochłonięte przez
  tarczę nie emituje `UnitDamaged`.
- `BattleView.HandleUnitDamaged` aktualizuje pasek HP, flash modelu i opcjonalny
  world-space damage effect. To właściwy punkt wejścia dla nowej prezentacji.
- Jednostki są dostępne przez `UnitViewRegistry`, więc pozycję tekstu można
  pobrać z aktualnego `UnitView.transform`, zamiast ze statycznego środka heksa.
- Scena `Battle` ma screen-space overlay `Canvas` oraz osobny
  `UnitStatusOverlayRoot`. Nowy system powinien użyć tego samego sposobu
  projekcji świata do UI, ale mieć oddzielny dynamiczny root i pulę.
- Projekt używa TextMesh Pro i ma już font SDF, który można wykorzystać bez
  dokładania assetów ani materiałów.

## 4. Najważniejsze decyzje architektoniczne

### 4.1. Krytyk należy do eventu konkretnego obrażenia

Rozszerzyć `BattleEvent` o read-only pole `IsCritical` i zmienić fabrykę:

```csharp
BattleEvent UnitDamaged(
    int targetId,
    int amount,
    int remainingHp,
    HexCoord targetHex,
    bool isCritical)
```

`DamageResolver` przekazuje do eventu flagę z rozliczanego `DamageRequest`.
Floating text reaguje wyłącznie na `UnitDamaged`; nie próbuje zapamiętywać ani
parować wcześniejszego `UnitCrit`.

Istniejący `UnitCrit` pozostaje na ten etap dla zgodności testów i ewentualnych
innych konsumentów. Publiczne wejście `DamageResolver.Resolve` emituje go raz dla
całego trafienia. Wewnętrzna ścieżka rozdziału przez Guard przenosi
`IsCritical` do obu rzeczywistych eventów `UnitDamaged`, ale nie emituje
duplikatów `UnitCrit`.

Semantyka pierwszej wersji:

- każde niekrytyczne `UnitDamaged` mapuje się na `Normal`;
- każde krytyczne `UnitDamaged` mapuje się na `Critical`;
- Guard pokazuje osobną liczbę przy każdej jednostce, która utraciła HP;
- obie części obrażenia z krytycznego trafienia Guard są prezentowane jako
  `Critical`;
- pełna absorpcja przez tarczę, invulnerability lub obrażenie równe zero nie
  emituje tekstu, ponieważ nie ma `UnitDamaged`;
- DoT, Mark i Special są `Normal`, dopóki ich `DamageRequest` nie ma
  `IsCritical`.

### 4.2. Typ tekstu jest pojęciem prezentacji

Dodać enum w warstwie prezentacji, niezależny od gameplayowego `DamageKind`:

```csharp
public enum FloatingDamageTextType
{
    Normal = 0,
    Critical = 1
}
```

Nie rozszerzać `DamageKind` o pojęcia wizualne. Dzięki temu w przyszłości można
dodać `Heal`, `Shield`, `Miss` lub `Immune` bez zmiany sposobu obliczania
obrażeń. Style przechowywać w małej serializowanej tablicy wpisów indeksowanej
przez `FloatingDamageTextType`; lookup zbudować raz w `Awake`, bez LINQ.

### 4.3. Osobny system screen-space UI

Nie rozszerzać `PooledBattleEffect`. Ten komponent jest efektem world-space
opartym na `MeshRenderer`, natomiast tekst wymaga TMP, projekcji do canvasa i
innego lifecycle.

Przepływ danych:

```text
DamageResolver
  -> BattleEvent.UnitDamaged(Amount, RemainingHp, IsCritical)
  -> BattleView
  -> BattleUnitPresenter
  -> FloatingDamageTextController
  -> poolowany FloatingDamageTextView (TMP)
```

Symulacja nie otrzymuje referencji do prefabów, fontów, kolorów ani czasu
animacji.

## 5. Proponowane komponenty

### 5.1. `FloatingDamageTextView`

Pasywny komponent prefabu UI zawierający:

- cache `RectTransform` i `TMP_Text`;
- `Play(amount, type, startPosition)`;
- `Tick(deltaTime)`, zwracający informację o zakończeniu;
- `Release()`, resetujący tekst, alpha, skalę, pozycję i aktywność.

Widok nie ma własnego `Update`, coroutines ani tweenów. Animacja jest liczona
prostą interpolacją:

- faza wejścia: szybkie dojście do docelowej skali;
- faza ruchu: stały lub łagodzony ruch o konfigurowaną liczbę pikseli w górę;
- faza wyjścia: fade w końcowej części czasu życia.

Do ustawienia liczby użyć bezalokacyjnego API TMP (`SetText` z placeholderem),
bez `amount.ToString()` i bez konkatenacji stringów w trakcie walki. `TMP_Text`
i tablica stylów `Normal`/`Critical` należą do prefabu, więc authoring fontu,
materiału, obrysu, rozmiaru, koloru i parametrów animacji odbywa się w jednym
assetcie bez zmiany kodu kontrolera.

Punkt wyjścia do tuningu na telefonie:

| Parametr | Normal | Critical |
|---|---:|---:|
| Czas życia | 0,65 s | 0,80 s |
| Ruch w górę | 48 px | 58 px |
| Skala start/docelowa | 0,90 / 1,00 | 1,30 / 1,10 |
| Kolor | jasny czerwony/biały | bursztynowy/żółty |
| Początek fade | 55% | 60% |

Są to wartości startowe, nie finalny art direction.

### 5.2. `FloatingDamageTextController`

Centralny komponent prezentacji odpowiedzialny za:

- referencje do prefabu, dynamicznego `RectTransform` root i kamery świata;
- projekcję punktu trafienia przez `WorldToScreenPoint` oraz
  `RectTransformUtility.ScreenPointToLocalPointInRectangle`;
- jedną listę aktywnych widoków i jeden `Stack` elementów wolnych;
- prewarm przed walką;
- jeden `LateUpdate`, który tickuje wyłącznie aktywne teksty;
- `SetCombatSpeed`, `Show`, `ReleaseAll` i cleanup w `OnDisable`;
- limit aktywnych elementów; po jego osiągnięciu kontrolowane ponowne użycie
  najstarszego tekstu zamiast dalszego `Instantiate`.

Początkowy budżet: prewarm 16–24 elementów, `maxActive` 32. Wartości powinny być
serializowane i dostrojone po stres teście maksymalnej liczby jednostek.

Pozycja świata jest próbkowana w momencie trafienia z aktualnego
`UnitView.transform` i zapisywana jako pozycja startowa UI. Tekst nie śledzi
dalej obiektu. Dzięki temu:

- śmiertelny tekst kończy animację mimo dezaktywacji modelu;
- nie wykonujemy projekcji świata dla każdego tekstu co klatkę;
- tekst nie jest przeciągany za jednostką, która zaczyna kolejny ruch.

Jeśli `UnitView` chwilowo nie jest dostępny, fallbackiem jest
`BoardPresenter.GetWorldPosition(battleEvent.To)`. Dodać stały, serializowany
`worldOffset`, aby tekst zaczynał nad bryłą jednostki, ale poniżej jej paska HP.

### 5.3. Deterministyczne rozdzielanie nakładających się trafień

Wiele obrażeń na tej samej jednostce w jednej paczce eventów nie może tworzyć
idealnie nakładających się napisów. Kontroler nadaje kolejnym tekstom mały,
cykliczny offset X, np. `0, -18, +18, -32, +32` px.

Nie używać `UnityEngine.Random`. Sekwencja ma być deterministyczna i resetowana
przy `ReleaseAll`. Nie agregować wartości w czasie — każdy `UnitDamaged`
pozostaje osobnym, audytowalnym komunikatem.

## 6. Integracja z istniejącym kodem

### `BattleEvent` i `DamageResolver`

- dodać `IsCritical` do kontraktu eventu;
- uzupełnić wszystkie fabryki eventów bez zmiany ich dotychczasowej semantyki;
- dodać prywatną ścieżkę rozliczenia Guard, która zachowa krytyczność na obu
  `UnitDamaged`, ale nie powieli `UnitCrit`;
- nie dodawać zależności od UI do `DamageResolver` ani `DamageRequest`.

### `BattleUnitPresenter`

- przy `HandleDamaged` nadal uruchamiać flash `UnitView`;
- wyznaczyć aktualny punkt startu z trafionego `UnitView`, z fallbackiem do
  pozycji heksa;
- przekazać `Amount` i mapowany typ do `FloatingDamageTextController`;
- nie formatować tekstu i nie zarządzać pulą w presenterze.

### `BattleView`

- dodać serializowaną referencję do `FloatingDamageTextController`;
- przekazać ją do `BattleUnitPresenter`;
- propagować `SetCombatSpeed`;
- wykonywać `ReleaseAll` w `BindInitialState`, `ClearBattle` i przy wyłączeniu
  widoku;
- nie zwalniać aktywnych tekstów natychmiast po `UnitDied` — liczba śmiertelnego
  trafienia ma dokończyć animację.

### Prefab i scena

Utworzyć `Assets/DeckBattle/Prefabs/Battle/PF_FloatingDamageText.prefab`:

- `RectTransform`;
- pojedynczy `TextMeshProUGUI`;
- `FloatingDamageTextView`;
- `raycastTarget = false`;
- bez `LayoutGroup`, `ContentSizeFitter`, `Animator` i osobnego `CanvasGroup`.

W scenie `Battle` dodać `FloatingDamageTextRoot` jako sibling
`UnitStatusOverlayRoot` pod głównym screen-space `Canvas`:

- rozciągnięty do pełnego recta;
- nad prezentacją jednostek/statusów, ale pod modalnymi ekranami HUD;
- z własnym zagnieżdżonym `Canvas`, bez `GraphicRaycaster`, aby częsta zmiana
  pozycji tekstów nie przebudowywała całego HUD;
- z podłączonym prefabem, kamerą świata, prewarm i limitem aktywnych widoków.

Scenę, prefab i referencje modyfikować przez Unity MCP, aby zachować poprawne
GUID-y, serializację i natychmiast sprawdzić brakujące referencje.

## 7. Wydajność mobilna

- zero `Instantiate`/`Destroy` po prewarmie w typowym przebiegu walki;
- zero per-frame GC Alloc po rozgrzaniu TMP i puli;
- jedna centralna pętla tylko po aktywnych napisach;
- brak LINQ, coroutines, closure, tween sequence i wyszukiwania komponentów w
  trakcie trafienia;
- brak nowych materiałów per tekst; wspólny font SDF i materiał;
- brak raycastów UI i layout rebuildów;
- twardy limit aktywnych tekstów zapobiega skokowi pamięci przy burst damage;
- oddzielny dynamiczny canvas ogranicza koszt przebudowy głównego HUD;
- alpha i skala są aktualizowane tylko przez czas życia aktywnego tekstu.

Do sprawdzenia w Profilerze na urządzeniu lub profilu mobile-like:

- `GC Alloc` podczas serii trafień po prewarmie;
- `Canvas.BuildBatch` i `UI.Rendering`;
- liczba aktywnych TMP submeshów i draw calli;
- brak wzrostu liczby GameObjectów po kolejnych rundach;
- frame pacing przy 1x i 2x oraz przy 16–32 jednoczesnych tekstach.

## 8. Testy

### Edit Mode — kontrakt symulacji

- niekrytyczne trafienie emituje `UnitDamaged.IsCritical == false`;
- krytyczne trafienie emituje `UnitDamaged.IsCritical == true`;
- pocisk zachowuje flagę krytyka aż do momentu impactu;
- krytyk rozdzielony przez Guard daje dwa krytyczne `UnitDamaged` i jeden
  `UnitCrit`;
- DoT/Mark bez flagi krytyka emituje `Normal`;
- pełna absorpcja przez shield oraz invulnerability nie emituje
  `UnitDamaged`;
- `Amount` i `RemainingHp` pozostają zgodne z obecnymi testami resolwera.

### Edit Mode — widok i pooling

- `Normal` i `Critical` wybierają poprawny styl;
- `Play` ustawia liczbę bez prefiksu tekstowego i resetuje stan poprzedniego
  użycia;
- `Tick` poprawnie przesuwa, skaluje, wygasza i kończy widok;
- `SetCombatSpeed(2)` dwukrotnie skraca czas rzeczywisty animacji;
- zakończony widok wraca do puli i jest ponownie używany;
- `ReleaseAll` czyści elementy aktywne oraz licznik offsetów;
- przekroczenie `maxActive` nie zwiększa liczby utworzonych instancji.

### Play Mode / weryfikacja sceny

- tekst pojawia się przy poprawnej jednostce dla melee i ranged;
- tekst krytyczny jest jednoznacznie odróżnialny na ekranie telefonu;
- DoT, Special, Mark i Guard pokazują właściwą liczbę tekstów;
- śmiertelny hit nie ucina tekstu po dezaktywacji `UnitView`;
- kilka trafień w jednym ticku nie nakłada się idealnie;
- teksty nie zasłaniają paska HP, kart ani komunikatu rundy na typowych
  proporcjach portrait;
- pauza/wyłączenie obiektu oraz nowa runda nie pozostawiają starych tekstów;
- przy 1x i 2x ruch oraz czas życia pozostają spójne z resztą prezentacji.

Po zmianach najpierw uruchomić wąskie testy Edit Mode przez Unity MCP, następnie
test Play Mode sceny `Battle` i manualny stres test. Nie uruchamiać Edit Mode w
batchmode.

## 9. Kolejność implementacji

1. Rozszerzyć kontrakt `BattleEvent.UnitDamaged` o `IsCritical` i uzupełnić
   testy `DamageResolver`/`ProjectileResolver`.
2. Uporządkować wewnętrzne rozliczanie Guard tak, aby zachować krytyczność bez
   duplikowania `UnitCrit`.
3. Dodać `FloatingDamageTextType`, serializowane style i pasywny
   `FloatingDamageTextView`.
4. Dodać `FloatingDamageTextController` z prewarmem, poolingiem, limitem,
   projekcją świata i deterministycznymi offsetami.
5. Utworzyć prefab oraz dynamiczny root/canvas w scenie przez Unity MCP.
6. Podłączyć controller do `BattleUnitPresenter` i lifecycle `BattleView`.
7. Dodać testy widoku, poolingu, cleanupu i prędkości walki.
8. Wykonać weryfikację melee/ranged/DoT/Guard/lethal oraz profil 1x/2x na
   docelowych proporcjach mobilnych.
9. Dostroić wyłącznie parametry stylu, czasu i offsetów po teście na urządzeniu;
   nie mieszać tego etapu z nowymi typami komunikatów.

## 10. Kryteria akceptacji

- każdy dodatni `UnitDamaged` wyświetla dokładnie jeden tekst przy właściwej
  jednostce;
- `Normal` i `Critical` korzystają z tego samego prefabu, lecz są natychmiast
  rozróżnialne;
- krytyczność pochodzi bezpośrednio z eventu konkretnego obrażenia, a nie z
  kolejności eventów;
- melee, pociski, DoT, Mark, Special i Guard korzystają z jednego przepływu;
- pełna tarcza/invulnerability nie pokazuje fałszywego `0`;
- śmiertelny tekst kończy animację;
- 2x przyspiesza animację bez rozjechania cleanupu;
- po prewarmie typowa walka nie wykonuje `Instantiate`, `Destroy` ani per-frame
  GC Alloc dla floating textów;
- liczba obiektów pozostaje stała pomiędzy rundami;
- układ jest czytelny na obsługiwanych proporcjach telefonów i nie wywołuje
  kosztownych przebudów głównego HUD;
- dodanie następnych typów prezentacji wymaga nowego wpisu stylu i obsługi
  odpowiedniego eventu, bez zmiany logiki obliczania obrażeń.

## 11. Szacunek

- kontrakt eventów i testy resolwera: 0,5 dnia;
- widok, animacja, pooling i testy: 0,5–1 dnia;
- prefab, scena, integracja i weryfikacja: 0,5 dnia;
- profilowanie oraz tuning na urządzeniu: 0,5 dnia.

Łącznie: około 2–2,5 dnia pracy wraz z testami i pierwszym tuningiem mobilnym.
