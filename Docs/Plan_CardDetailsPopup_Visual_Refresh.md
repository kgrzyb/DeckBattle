# Plan odświeżenia `CardDetailsPopup`

## 1. Cel i ustalone decyzje

Popup szczegółów karty ma szybciej przekazywać najważniejsze informacje na
telefonie i nie budować opisów mechanik w kodzie UI.

Ustalenia:

- każda statystyka jednostki pokazuje ikonę i wyłącznie wartość;
- AP pokazuje wyłącznie wartość, bez napisu `AP` i bez ikony;
- szansa na trafienie krytyczne i mnożnik trafienia krytycznego są osobnymi
  statystykami;
- próg many i mana co tick są osobnymi statystykami;
- `SPECIAL` i `ON PLAY` pozostają osobnymi tekstami nagłówków;
- pod nagłówkiem wyświetlany jest tylko ręcznie napisany opis;
- wartości w opisach są obliczane dla konkretnej jednostki przed wstawieniem do
  tekstu, zamiast pokazywania pól konfiguracyjnych takich jak mnożnik;
- odwołania do statusów na pierwszym etapie pokazują `DisplayName`; architektura
  ma pozwolić później zastąpić nazwę ikoną z fallbackiem do nazwy.

Zmiana nie obejmuje przebudowy logiki walki, balansu ani popupu spelli poza
koniecznymi zmianami wspólnego layoutu.

## 2. Stan obecny

`Assets/DeckBattle/Scripts/UI/CardDetailsPopupView.cs`:

- formatuje statystyki jako teksty typu `HP 450`, `Attack 30` i `Armor 0%`;
- łączy szansę i mnożnik krytyczny w jednym polu;
- łączy trzy informacje o manie w jednym polu;
- dodaje prefiks `AP` do kosztu;
- pokazuje statyczne `UnitOnPlayEffectDefinition.Description`;
- nie posiada pola ani prezentacji opisu speciala;
- tworzy część brakującego layoutu programowo w `EnsureLayout`.

`UnitSpecialDefinition` nie ma danych prezentacyjnych. Współdzielony asset
`UnitOnPlayEffectDefinition` ma ręczny opis, lecz nie obsługuje podstawiania
wartości.

W scenie i katalogu UI znajdują się niezacommitowane zmiany użytkownika, w tym
złota rama `CardDetailsPopup_Background.png`. Implementacja musi je zachować i
zostać wykonana na ich aktualnej wersji, najlepiej przez Unity MCP.

## 3. Docelowa hierarchia wizualna

### 3.1. Nagłówek

- grafika karty po lewej;
- nazwa jednostki jako główny tekst;
- typ i rzadkość jako krótkie informacje pomocnicze;
- koszt AP w istniejącym medalionie/badge'u, ale jako sama liczba, np. `3`.

### 3.2. Siatka statystyk

Użyć statycznej siatki ikon i wartości. Nie stosować `LayoutGroup` wymagającego
częstych przebudów. Każdy element składa się z `Image` oraz
`TextMeshProUGUI` i ma wspólny rozmiar ikony, font oraz odstęp.

Docelowe elementy:

| Statystyka | Tekst przykładowy | Ikona w atlasie |
| --- | --- | --- |
| HP | `450` | serce |
| Attack | `30` | miecz |
| Power | `1` | magiczny rozbłysk |
| Range | `2` | radar ze strzałką |
| Crit Chance | `10%` | celownik |
| Crit Multiplier | `2×` | podwójny rozbłysk uderzenia |
| Attack Speed | `1.25/s` | uskrzydlony miecz |
| Mana Threshold | `100` | kryształ many |
| Mana per Tick | `+3` | kryształ many |
| Armor | `20%` | tarcza |
| Armor Penetration | `15%` | przebita tarcza |

Nazwy statystyk nie trafiają do widocznego tekstu. Znaczenie ikon można później
uzupełnić tooltipem po przytrzymaniu, ale nie jest to część pierwszego wdrożenia.

### 3.3. Opisy umiejętności

Dolna część popupu zawiera dwa niezależne bloki:

```text
SPECIAL
<ręczny opis po rozwiązaniu placeholderów>

ON PLAY
<ręczny opis po rozwiązaniu placeholderów>
```

Nagłówki mają stały styl, natomiast opisy korzystają z word wrappingu i mogą
zajmować kilka linii. Jeśli jednostka nie ma speciala lub `OnPlay`, cały
odpowiedni blok, łącznie z nagłówkiem, jest ukrywany. Drugi blok wykorzystuje
odzyskane miejsce.

Pod tekstem można zastosować delikatne, półprzezroczyste ciemne pole albo cienki
złoty separator. Nie dodawać osobnych ciężkich teł i materiałów dla każdej
statystyki.

## 4. Przygotowany zestaw ikon

Wygenerowano arkusz 4 x 3 dopasowany do złoto-brązowej ramy popupu:

`Assets/DeckBattle/Art/Textures/UI/CardStats/CardStatIcons.png`

Kolejność ikon odpowiada tabeli z sekcji 3.2. Źródło ma rozdzielczość
1536 x 1024 i kanał alfa. Przed podpięciem do UI należy:

1. ustawić `Texture Type: Sprite (2D and UI)`;
2. ustawić `Sprite Mode: Multiple`;
3. pociąć arkusz na siatkę 4 x 3 i nadać sprite'om stabilne, domenowe nazwy;
4. wyłączyć mipmapy;
5. ustawić brak Read/Write po imporcie;
6. dobrać kompresję mobilną po sprawdzeniu czytelności cienkich krawędzi;
7. włączyć atlasowanie razem z pozostałymi elementami UI, jeśli projektowy
   `SpriteAtlas` jest już używany albo zostanie dodany dla tego ekranu.

Nie tworzyć ikony AP.

## 5. Model danych opisów

### 5.1. Special

Rozszerzyć `UnitSpecialDefinition` o:

```csharp
[TextArea]
public string DescriptionTemplate;
```

Opis pozostaje na współdzielonym assetcie speciala. Formatter otrzymuje również
`UnitDefinition`, ponieważ część końcowych wartości zależy od statystyk
konkretnej jednostki.

### 5.2. OnPlay

Zmienić semantykę pola opisu w `UnitOnPlayEffectDefinition` na szablon. Przy
zmianie nazwy pola użyć `FormerlySerializedAs("Description")`, aby zachować
istniejącą treść assetu.

Opis nadal należy do współdzielonego assetu efektu. Nie duplikować go w każdej
`UnitDefinition`.

## 6. Formatter szablonów

Dodać małą, czystą klasę C#, np. `CardDescriptionTemplateFormatter`. Nie używać
refleksji ani ogólnego systemu skryptowego. Formatter ma jawny, ograniczony
słownik tokenów i działa wyłącznie przy `Show/Apply` popupu.

### 6.1. Zasada wartości końcowych

Tokeny prezentują wynik zrozumiały dla gracza, a nie surową konfigurację.
Przykładowo special `AttackDamageMultiplier = 2` jednostki z `Attack = 30`
powinien wstawić `60` obrażeń na trafienie, a nie `2` ani `200%`.

Popup nie zna celu ani runtime'owych statusów. Dlatego „wartość końcowa” w tym
kontekście oznacza bazowy wynik przed pancerzem celu, penetracją, buffami,
debuffami i innymi zmianami podczas walki. Nie wolno przedstawiać jej jako
gwarantowanych obrażeń zadanych konkretnemu celowi.

Obliczenia podglądowe muszą używać tej samej kolejności działań i zaokrąglenia co
logika walki. Wspólną małą metodę obliczającą bazowe obrażenia przed mitygacją
należy wyodrębnić z `DamageCalculator`, aby UI i symulacja nie utrzymywały dwóch
różnych wzorów.

### 6.2. Proponowane tokeny speciala

- `{damagePerHit}` - bazowe obrażenia jednego trafienia;
- `{totalDamage}` - bazowa suma obrażeń wszystkich trafień;
- `{strikeCount}` - liczba trafień;
- `{castDuration}` - gotowy tekst czasu, np. `1.5 s`;
- `{status}` - referencja do statusu;
- `{statusDuration}` - gotowy tekst czasu;
- `{statusMagnitude}` - liczba odpowiednia dla statusu;
- `{statusMagnitudePercent}` - gotowy procent.

Nie udostępniać tokenów surowego mnożnika, jeśli gracz powinien zobaczyć
obliczone obrażenia.

Przykłady:

```text
Wykonuje {strikeCount} szybkich ciosów, zadając po {damagePerHit} obrażeń.
```

```text
Zyskuje {status} na {statusDuration}, skracając czas między atakami o {statusMagnitudePercent}.
```

### 6.3. Proponowane tokeny OnPlay

Tokeny kroków są numerowane od 1, np.:

- `{step1.amount}`;
- `{step1.percent}`;
- `{step1.attackBonus}` - obliczony przyrost dla bieżącej jednostki;
- `{step1.attackAfterEffect}` - bazowa wartość po zastosowaniu efektu;
- `{step1.status}`;
- `{step1.statusDuration}`;
- `{step1.statusMagnitude}`;
- `{step1.target}` tylko wtedy, gdy ręczny opis rzeczywiście potrzebuje nazwy
  celu.

Przykład:

```text
Po zagraniu zyskuje {step1.attackBonus} bazowego Ataku na następną walkę.
```

Jeżeli procent nie daje całkowitej liczby przed końcowym zaokrągleniem walki,
token powinien użyć tej samej reguły zaokrąglenia co obliczenie obrażeń, a test
powinien dokumentować wynik.

### 6.4. Błędy i brakujące dane

- pusty szablon daje pusty opis i ukrywa blok;
- brak wymaganego statusu nie może rzucać wyjątku;
- nieznany token pozostaje widoczny w buildzie jako sygnał błędu contentu albo
  zwraca kontrolowany placeholder; wybrać jedną politykę i pokryć ją testem;
- test edytorowy skanuje produkcyjne assety i zgłasza nieznane tokeny przed
  buildem;
- nie logować tego samego błędu co klatkę ani przy każdym odświeżeniu tekstu.

## 7. Statusy: nazwa teraz, ikona później

Token `{status}` nie powinien bezpośrednio odczytywać `DisplayName`. Powinien
delegować do małej warstwy prezentacji, np. `StatusReferenceFormatter`.

Etap 1:

- zwraca bezpiecznie `StatusDefinition.DisplayName`;
- przy pustej nazwie używa stabilnego fallbacku, np. `Kind.ToString()`.

Etap docelowy:

- zwraca znacznik TMP `<sprite name="haste">` wskazujący ikonę statusu;
- sprite jest pobierany z jednego `TMP_SpriteAsset` o stabilnych nazwach;
- jeśli status nie ma ikony albo mapping jest niepełny, pozostaje nazwa;
- obecne pole `StatusDefinition.Icon` może być źródłem do budowy katalogu, ale
  nie należy tworzyć osobnego `Image` w środku zdania.

Dzięki temu ręczne szablony i ich tokeny nie zmieniają się podczas migracji z
nazw na ikony.

## 8. Zmiany w `CardDetailsPopupView`

1. Dodać serializowane referencje do 12 par `Image` + `TextMeshProUGUI`.
2. Rozdzielić dotychczasowe pola `critText` i `manaText` zgodnie z tabelą.
3. Ustawić `apCostText` na samo `definition.ApCost`.
4. Dodać osobne rooty i teksty:
   - `specialRoot`, `specialHeaderText`, `specialDescriptionText`;
   - `onPlayRoot`, `onPlayHeaderText`, `onPlayDescriptionText`.
5. W `ApplyUnitDetails` przypisać ikony i sformatowane wartości bez nazw.
6. Opisy formatować z kontekstem `UnitDefinition` i odpowiedniej definicji
   efektu.
7. W `ClearUnitDetails` czyścić wszystkie nowe pola.
8. W ścieżce spella ukrywać kompletny root jednostki bez przełączania każdego
   dziecka osobno, jeśli pozwala na to ostateczna hierarchia.
9. Zachować tworzenie brakujących elementów w `EnsureLayout` dla testów, ale
   scena produkcyjna powinna mieć jawnie ustawione referencje i layout.

Nie dodawać `Update`, LINQ ani powtarzalnych wyszukiwań komponentów.

## 9. Kolejność implementacji

### Etap A - dane i czysta logika

1. Dodać `DescriptionTemplate` do definicji speciala i OnPlay z bezpieczną
   migracją serialized data.
2. Wyodrębnić wspólne bazowe obliczenie obrażeń przed mitygacją.
3. Dodać formatter szablonów oraz formatter referencji statusu.
4. Uzupełnić ręczne szablony produkcyjnych assetów.
5. Dodać testy formattera i obliczeń.

### Etap B - ikony i layout

1. Zaimportować i pociąć przygotowany atlas.
2. Zbudować statyczną siatkę 12 elementów w `CardDetailsPopup`.
3. Podłączyć nagłówki oraz opisy `SPECIAL` i `ON PLAY`.
4. Podłączyć referencje w `CardDetailsPopupView`.
5. Sprawdzić kontrast i czytelność na docelowej ramie.

### Etap C - weryfikacja

1. Uruchomić w Unity MCP kompilację oraz najwęższe testy Edit Mode.
2. Sprawdzić jednostkę z `HasteBurst`, jednostkę z `FurySwipes`, brak speciala,
   OnPlay i kartę spella.
3. Sprawdzić typowe proporcje telefonu, safe area i największe wartości.
4. W profilu mobilnym potwierdzić brak aktualizacji lub alokacji co klatkę.

## 10. Testy akceptacyjne

- AP pokazuje `3`, nigdy `AP 3`;
- żaden tekst statystyki nie zawiera nazwy statystyki;
- Crit Chance i Crit Multiplier mają osobne ikony i wartości;
- trzy wartości many mają osobne ikony i wartości;
- jednostka z Attack `30` i mnożnikiem speciala `2` może wstawić
  `{damagePerHit}` jako `60`;
- wynik obliczenia korzysta z tej samej reguły zaokrąglenia co walka;
- Haste wstawia nazwę statusu oraz rzeczywisty czas i magnitude z assetu;
- `SPECIAL` i `ON PLAY` są osobnymi nagłówkami;
- pusty special lub OnPlay ukrywa także jego nagłówek;
- nieznany token jest wykrywany przez test contentu;
- spell nie pokazuje żadnej statystyki jednostki ani jej opisów;
- ponowne otwieranie popupu nie dodaje kolejnych obiektów UI;
- wygląd pozostaje czytelny przy najwęższym wspieranym ekranie i safe area.

## 11. Ryzyka

- bazowe obrażenia w opisie mogą zostać błędnie odczytane jako gwarantowane
  obrażenia po pancerzu; treść ręcznego opisu powinna unikać takiej sugestii;
- ręcznie zduplikowany wzór obliczeń szybko rozjedzie się z walką, dlatego
  wymagane jest wspólne źródło obliczenia;
- automatyczne cięcie atlasu musi zostać sprawdzone wizualnie, ponieważ ikony
  mają różne szerokości sylwetek;
- zbyt małe ikony stracą detale; po teście na urządzeniu może być potrzebne
  uproszczenie albo ręczne kadrowanie pojedynczych sprite'ów;
- przejście statusów na inline sprite'y wymaga spójnego `TMP_SpriteAsset` i
  fallbacku, inaczej opis może pokazać brakujący glif.

## 12. Prompt użyty do wygenerowania ikon

Tryb: wbudowany `imagegen`, z `CardDetailsPopup_Background.png` użytym wyłącznie
jako referencja stylu i palety.

```text
Use case: stylized-concept
Asset type: mobile fantasy card-battle game UI sprite sheet
Input images: Image 1 is a style and palette reference only; do not edit or reproduce its frame.
Primary request: Create a production-ready sprite sheet containing exactly 12 separate stat icons arranged in a strict 4-column by 3-row grid. Each icon must be centered inside its own equal invisible cell with generous empty padding and no overlap.
Icon order, left to right:
Row 1: health (strong heart), attack (single sword), power (compact magical burst), attack range (radar rings with outward arrow).
Row 2: critical chance (crosshair target), critical multiplier (double impact burst, no letters or numbers), attack speed (winged blade), maximum mana / mana threshold (faceted blue mana crystal).
Row 3: mana gained per attack (same mana crystal with small sword overlay), mana gained per damage taken (same mana crystal with small shield-impact overlay), armor (solid shield), armor penetration (arrow piercing a shield).
Style/medium: hand-painted fantasy mobile game UI icons; bold readable silhouettes; crisp edges; limited internal detail; consistent perspective and outline weight; polished but lightweight. Match the warm gold, amber, dark brown and subtle painted shading language of the reference frame. Use restrained semantic accents: red health, orange attack, violet power, cyan range, gold critical, pale blue speed, blue mana, steel armor. Icons must still feel like one coherent set.
Composition/framing: landscape 3:2 sprite sheet, exact 4x3 layout, equal cell sizes, each icon isolated and fully visible, no cell borders.
Scene/backdrop: perfectly flat solid #00ff00 chroma-key background for local removal. The entire background must be exactly one uniform green color.
Constraints: no text, no letters, no numbers, no labels, no decorative frames, no badges, no cell backgrounds, no drop shadows, no cast shadows, no reflections, no glow extending far from silhouettes, no watermark, no logos. Do not use #00ff00 anywhere inside the icons. Maintain strict order and exactly 12 icons.
```
