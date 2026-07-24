# Plan: Polish bitewny i czytelność MVP

## Cel

Poprawić czytelność i ergonomię sceny `Battle` po spięciu pełnej pętli MVP. Ten etap ma ułatwić testowanie gry na telefonie: gracz powinien rozumieć stan rundy, dostępne akcje, koszty kart, limit jednostek, wynik walki i powód blokady akcji. Zmiany powinny być lekkie wydajnościowo i nie powinny zmieniać zasad symulacji walki.

## Zależności

Ten etap najlepiej robić po:

- `PlayerProfile`,
- Deck Builderze,
- starcie bitwy z aktywnym deckiem,
- Booster Result po zwycięstwie.

Można wykonywać część polishu wcześniej, ale pełny sens ma dopiero wtedy, gdy da się przejść całą pętlę MVP.

## Zakres

- Lepszy feedback wyboru kart.
- Lepszy feedback dozwolonych pól planszy.
- Czytelniejsze stany przycisku `Ready`.
- Komunikaty powodów nieudanej akcji.
- Czytelniejsze AP, HP, runda i limit jednostek.
- Poprawa wyniku rundy i meczu.
- Przegląd UI pod portrait mobile i safe area.
- Ograniczenie niepotrzebnych odświeżeń UI.

Ten etap nie obejmuje:

- przebudowy zasad walki,
- nowych typów kart,
- dużych animacji,
- ciężkiego post-processingu,
- nowych shaderów wymagających profilingu,
- pełnego stylu artystycznego gry.

## Obszary pracy

### Feedback kart

Karta w ręce powinna jasno pokazywać:

- koszt AP,
- nazwę,
- typ,
- podstawowe statystyki,
- czy można ją aktualnie zagrać,
- czy jest wybrana.

Stany:

- normalna,
- wybrana,
- niedostępna przez brak AP,
- niedostępna przez limit jednostek,
- niedostępna po kliknięciu `Ready`.

Nie odświeżać tekstów i layoutu co klatkę. `BattleUIController` powinien aktualizować widok tylko po zmianie danych.

### Feedback planszy

Po wybraniu karty lub jednostki plansza powinna pokazać:

- pola, na których można zagrać jednostkę,
- pola zajęte,
- pola niedostępne,
- aktualnie wskazane pole.

Na MVP wystarczą proste zmiany materiału/koloru na `HexTileView`. Unikać drogich efektów przezroczystości i dużych obszarów overdraw.

### Ready i faza przygotowania

Przycisk `Ready` powinien jasno komunikować:

- że faza przygotowania trwa,
- że gracz jest gotowy,
- że trwa countdown do walki, jeśli przeciwnik jeszcze nie skończył,
- że podczas gotowości nie można już zagrywać kart.

Warto dodać krótki tekst statusu, np.:

- `Preparation`,
- `Ready`,
- `Combat`,
- `Round Result`,
- `Victory`,
- `Defeat`.

### Powody blokady akcji

Gdy akcja się nie uda, UI powinno pokazać krótki powód:

- za mało AP,
- deck lub ręka nie zawiera karty,
- limit jednostek osiągnięty,
- pole zajęte,
- pole poza strefą wystawiania,
- gracz już kliknął `Ready`.

Te komunikaty powinny pochodzić z istniejących enumów fail reason, a nie z osobnej logiki UI.

### Wynik rundy i meczu

Po walce gracz powinien zobaczyć:

- kto zadał obrażenia po rundzie,
- ile HP zostało obu stronom,
- czy mecz trwa dalej,
- wynik końcowy.

Nie trzeba robić pełnego combat logu. Wystarczy krótki, czytelny overlay rundy.

### Mobile layout

Sprawdzić:

- portretowe proporcje telefonu,
- safe area,
- czy tap targety są wystarczająco duże,
- czy ręka kart nie przykrywa krytycznych pól,
- czy tekst mieści się w przyciskach i panelach,
- czy wynik meczu i booster nie nachodzą na UI bitwy.

## Proponowane klasy i zmiany

### BattleActionFeedbackView

Lekki komponent do krótkich komunikatów akcji.

Przykładowe API:

```csharp
void ShowMessage(string message);
void Clear();
```

Powinien być sterowany zdarzeniami z kontrolera, bez logiki zasad gry.

### BoardHighlightController

Komponent lub część `BoardPresenter`, która zarządza highlightami pól.

Odpowiedzialność:

- wyczyścić poprzednie highlighty,
- oznaczyć dostępne pola,
- oznaczyć pole hover/touch,
- nie alokować list w gorącej ścieżce inputu.

### CardInteractabilityState

Mały enum lub model stanu karty dla UI.

Przykłady:

- `Playable`,
- `NotEnoughAp`,
- `BoardLimitReached`,
- `PlayerReady`,
- `Selected`.

Można go budować w `BattleUIController` lub osobnym helperze, ale bez duplikowania walidacji z `UnitPlayService` i `SpellPlayService`.

## Wydajność

Przy polishu obowiązują ograniczenia mobilne:

- bez layout rebuildów w `Update`,
- bez LINQ w hot path,
- bez tworzenia nowych materiałów per klik,
- preferować `MaterialPropertyBlock` albo predefiniowane materiały,
- cache komponentów UI,
- nie robić ciągłych string concatenation w licznikach,
- aktualizować tekst tylko, gdy wartość się zmieniła.

## Testy i weryfikacja

Testy EditMode:

- mapowanie fail reason na komunikaty UI,
- stany interaktywności kart,
- logika highlightów pól, jeśli zostanie wydzielona do plain C#.

Weryfikacja manualna w Unity:

- uruchomić scenę `Battle`,
- zagrać kartę przy wystarczającym AP,
- spróbować zagrać kartę bez AP,
- spróbować zagrać kartę po `Ready`,
- spróbować wystawić jednostkę na niedozwolonym polu,
- zakończyć rundę i sprawdzić overlay wyniku,
- zakończyć mecz i sprawdzić `Victory`/`Defeat`.

Jeśli Unity Editor jest otwarty, testy EditMode uruchamiać przez `DeckBattle > Tests > Run EditMode Tests`, zgodnie z `Docs/Testing.md`.

## Kolejność implementacji

1. Spisać obecne punkty bólu w scenie `Battle` po przejściu pełnej pętli MVP.
2. Dodać mapowanie powodów błędów na krótkie komunikaty UI.
3. Dodać lub poprawić stany interaktywności kart.
4. Dodać highlight dozwolonych pól planszy.
5. Poprawić status fazy i przycisk `Ready`.
6. Poprawić overlay wyniku rundy i meczu.
7. Sprawdzić layout na typowych proporcjach telefonu.
8. Usunąć zbędne odświeżenia UI i potencjalne alokacje.
9. Uruchomić wąskie testy i wykonać manualną weryfikację sceny.

## Ryzyka i decyzje do sprawdzenia

- Polish nie może ukryć problemów zasad gry przez osobną logikę walidacji w UI.
- Highlight pól nie powinien tworzyć nowych materiałów runtime dla każdego pola.
- UI musi pozostać czytelne przy małych ekranach i notchach.
- Animacje powinny być krótkie i opcjonalne, bez wpływu na frame pacing.
- Combat feedback nie powinien zamienić się w pełny log, dopóki podstawowa pętla nie jest przetestowana.

## Definicja ukończenia

Etap jest gotowy, gdy:

- gracz widzi, które karty może zagrać,
- gracz widzi, gdzie może wystawić lub przesunąć jednostkę,
- nieudane akcje pokazują jasny powód,
- `Ready`, faza walki i wynik rundy są czytelne,
- UI działa w portretowym układzie telefonu z safe area,
- odświeżenia UI nie dzieją się niepotrzebnie w każdej klatce,
- zmiany nie modyfikują zasad walki,
- podstawowy scenariusz meczu można przejść ręcznie bez niejasnych stanów UI.
