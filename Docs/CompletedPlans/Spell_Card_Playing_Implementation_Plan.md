# Deck Battle - Spell Card Playing Implementation Plan

## 1. Cel

Dodac obsluge zagrywania kart zaklec z reki gracza w fazie przygotowania.

Karta zaklecia ma korzystac z podobnego flow jak karta jednostki:

- tapniecie karty pokazuje szczegoly i wybiera karte,
- przytrzymanie i drag pokazuje ghost karty,
- drop albo tap na poprawny cel zagrywa karte,
- po zagraniu karta znika z reki, koszt AP jest pobierany, a efekt jest aplikowany.

Zaklecia sa zagrywane tylko w `BattlePhase.Preparation`.

## 2. Decyzje Zakresu

### Targetowanie

Przy zagrywaniu kart zaklec potrzebne sa tylko dwa tryby:

```text
SpellTargetingKind
  - None
  - FriendlyUnit
```

Nie dodawac `AnyUnit` ani `EnemyUnit`. W fazie przygotowania jednostki przeciwnika sa ukryte, a projektowo zaklecia gracza nie beda targetowac enemy unit ani dowolnej jednostki.

### Zaklecia bez celu

Projekt musi wspierac zaklecia, ktore nie wymagaja celu.

Przyklad zachowania:

- tap karty pokazuje szczegoly i wybiera karte,
- drugi tap na planszy zagrywa zaklecie bez celu,
- drag karty i drop na plansze rowniez zagrywa zaklecie bez celu.

Dla zaklec bez celu nie podswietlac wszystkich heksow, bo sugerowaloby to targetowanie pola. Wystarczy stan wybranej karty i ghost podczas dragowania.

### Zaklecia z celem

Zaklecie `FriendlyUnit` moze zostac zagrane tylko na zywa jednostke gracza.

Podczas wyboru albo dragowania takiego zaklecia nalezy podswietlic tylko heksy, na ktorych stoja legalne jednostki gracza.

## 3. Aktualne Punkty Integracji

W projekcie istnieja juz podstawowe elementy spell gameplayu:

- `Assets/DeckBattle/Scripts/Cards/SpellPlayService.cs`
- `Assets/DeckBattle/Scripts/Cards/SpellTarget.cs`
- `Assets/DeckBattle/Scripts/Data/SpellDefinition.cs`
- `Assets/DeckBattle/Scripts/Data/SpellTargetingKind.cs`
- `Assets/DeckBattle/Scripts/Battle/BattleController.cs`
- `Assets/DeckBattle/Scripts/Input/BattleInputController.cs`
- `Assets/DeckBattle/Scripts/Board/BoardPresenter.cs`
- `Assets/DeckBattle/Scripts/UI/CardDetailsPopupView.cs`

Glowny brak jest w warstwie inputu i podswietlen planszy: `BattleInputController` nadal traktuje grywalna karte jako `CardKind.Unit`, a `BoardPresenter.HighlightCardPlayableTiles(...)` nie pokazuje celow zaklec.

## 4. Zmiany W Modelu Danych

Rozszerzyc `SpellTargetingKind`:

```text
None
FriendlyUnit
```

Nie dodawac szerszych trybow targetowania.

Zweryfikowac `SpellDefinition.OnValidate()`:

- `Amount >= 0`,
- `CardKind` pozostaje `Spell`,
- jesli w edytorze powstana niekompatybilne ustawienia efektu i targetowania, wykryc je w walidacji gameplayu, nie wymuszac tego samym UI.

## 5. SpellPlayService

Rozszerzyc `SpellPlayService.ValidatePlay(...)`:

- sprawdzic `battleState`, `player`, typ karty, faze preparation, ready state, hand i AP tak jak obecnie,
- dla `SpellTargetingKind.None` zaakceptowac brak `target.Unit`,
- dla `SpellTargetingKind.FriendlyUnit` wymagac zywej jednostki nalezacej do gracza,
- odrzucic niekompatybilny efekt i targetowanie.

Minimalna kompatybilnosc efektow:

```text
BuffAttackNextCombat -> wymaga FriendlyUnit
```

Jesli pozniej dojdzie efekt globalny, moze uzywac `None`.

Rozszerzyc `PlaySpell(...)` tak, aby nie zakladal zawsze `target.Unit != null`. Dla efektow bez celu aplikacja efektu musi byc osobna sciezka.

## 6. Input I Zagrywanie

W `BattleInputController` zastapic obecne `CanPrepareUnitCard(...)` bardziej ogolnym sprawdzeniem:

```text
CanPreparePlayableCard(state, card)
```

Warunki:

- `PreparationTurnService.CanPlayerPrepare(state)`,
- karta istnieje i jest w rece,
- `CardKind.Unit` z `UnitDefinition` albo `CardKind.Spell` z `SpellDefinition`.

### Drag

Podczas `BeginCardDrag(...)` pozwolic startowac drag zarowno dla jednostek, jak i zaklec.

Podczas `UpdateCardDrag(...)`:

- dla jednostki zachowac obecne sprawdzanie `UnitPlayService.ValidatePlay(...)`,
- dla zaklecia `FriendlyUnit` sprawdzic, czy pod kursorem/palcem jest heks z legalna jednostka gracza,
- dla zaklecia `None` nie wymagac konkretnego heksa jako celu, ale mozna wymagac dropu nad plansza, zeby uniknac przypadkowego zagrania poza obszarem gry.

Podczas `EndCardDrag(...)`:

- `Unit` -> `battleController.TryPlayPlayerCard(card, coord)`,
- `Spell/FriendlyUnit` -> `battleController.TryPlayPlayerSpell(card, SpellTarget.ForUnit(unit))`,
- `Spell/None` -> `battleController.TryPlayPlayerSpell(card, default(SpellTarget))` albo dedykowany `SpellTarget.None`, jezeli zostanie dodany dla czytelnosci.

### Tap-To-Select

Po tapnieciu karty:

- pokazac szczegoly,
- zaznaczyc karte w rece,
- dla jednostki podswietlic legalne heksy deploymentu,
- dla zaklecia `FriendlyUnit` podswietlic legalne cele,
- dla zaklecia `None` nie podswietlac heksow.

Po tapnieciu planszy przy wybranej karcie:

- jednostka: obecne zagranie na heks,
- zaklecie `FriendlyUnit`: zagranie na jednostke stojaca na tapnietym heksie,
- zaklecie `None`: zagranie bez celu.

## 7. Podswietlanie Planszy

Rozszerzyc `BoardPresenter.HighlightCardPlayableTiles(...)`.

Logika:

- `CardKind.Unit` -> obecne `HighlightUnitPlayableTiles(...)`,
- `CardKind.Spell` i `TargetingKind.FriendlyUnit` -> nowe `HighlightSpellTargetTiles(...)`,
- `CardKind.Spell` i `TargetingKind.None` -> `ClearAllHighlights()`.

`HighlightSpellTargetTiles(...)`:

- wyczyscic hover highlight,
- przejsc po `state.Player.Units`,
- pominac null i martwe jednostki,
- dla kazdej jednostki sprawdzic `SpellPlayService.ValidatePlay(...)`,
- pobrac `HexTileView` po `unit.BattleCoord`,
- ustawic legal highlight.

Uzyc prostych petli i istniejacych kolekcji. Nie uzywac LINQ ani tymczasowych list w sciezce drag/hover.

## 8. Helper Celu Zaklecia

Dla czytelnosci wydzielic maly helper, najlepiej w warstwie inputu albo jako statyczny utility, jesli bedzie uzywany w testach:

```text
TryFindFriendlyUnitAtCoord(PlayerBattleState player, HexCoord coord, out RuntimeUnit unit)
```

Warunki:

- jednostka nalezy do `player`,
- `unit.IsAlive`,
- `unit.BattleCoord == coord`.

Nie skanowac jednostek przeciwnika, bo nie sa potrzebne dla zaklec gracza.

## 9. BattleController

`BattleController.TryPlayPlayerSpell(...)` juz istnieje i powinien pozostac publicznym punktem wykonania.

Sprawdzic, czy po udanym zagraniu:

- `EvaluatePreparationCountdownState()` jest wolane,
- `ProgressAutomaticFlow()` jest wolane,
- `RefreshUnits()` aktualizuje widoki jednostek,
- `RaiseStateChanged()` odswieza UI.

Dla zaklec zmieniajacych widoczne statystyki jednostki moze byc potrzebne dodatkowe odswiezenie status overlay, jesli overlay pokazuje dana wartosc.

## 10. Testy

Rozszerzyc edit mode tests.

### SpellPlayService

Dodac przypadki:

- `ValidatePlay` akceptuje zaklecie `None`, gdy efekt nie wymaga jednostki,
- `ValidatePlay` odrzuca `FriendlyUnit` bez celu,
- `ValidatePlay` odrzuca martwa jednostke,
- `ValidatePlay` odrzuca jednostke spoza gracza,
- `ValidatePlay` odrzuca `BuffAttackNextCombat` z `TargetingKind.None`,
- `PlaySpell` nadal pobiera AP i przenosi karte do `PlayedCards`,
- zaklecie nadal jest odrzucane poza `BattlePhase.Preparation`.

### Input/Target Helper

Jesli helper zostanie wydzielony poza `MonoBehaviour`, dodac testy:

- znajduje zywa jednostke gracza po coord,
- ignoruje martwe jednostki,
- zwraca false dla pustego heksa.

### Regresja Jednostek

Po zmianach inputu upewnic sie, ze obecne testy zagrywania jednostek nadal przechodza.

## 11. Widok Szczegolowy Karty Zaklecia

Ten etap wykonac na koncu, po dzialajacym gameplayu.

Obecny `CardDetailsPopupView` miesza pola jednostki i zaklecia. Docelowo rozdzielic prezentacje na osobne sekcje:

```text
CardDetailsPopupView
  - common header
  - unitDetailsRoot
  - spellDetailsRoot
```

Sekcja wspolna:

- nazwa,
- koszt AP,
- typ karty,
- rzadkosc,
- grafika karty.

Sekcja jednostki:

- HP,
- Attack,
- Power,
- Range,
- Crit,
- Cooldown,
- Mana,
- Armor,
- Armor Penetration.

Sekcja zaklecia:

- target: `Bez celu` albo `Wlasna jednostka`,
- efekt,
- wartosc efektu,
- krotki opis dzialania.

Dla zaklec nie pokazywac pustych pol statystyk jednostki. UI powinien aktywowac tylko odpowiednia sekcje, zeby uniknac mylacych etykiet.

## 12. Kolejnosc Implementacji

1. Dodac `SpellTargetingKind.None`.
2. Rozszerzyc `SpellPlayService` o targetowanie `None` i kompatybilnosc efektow.
3. Dodac helper znajdowania friendly unit po heksie.
4. Rozszerzyc `BattleInputController` o dispatch unit/spell.
5. Rozszerzyc `BoardPresenter` o highlight celow zaklec.
6. Dodac testy logiki zaklec i helpera celu.
7. Zweryfikowac w Unity kompilacje i podstawowy flow drag/tap.
8. Na koncu rozdzielic szczegolowy widok karty jednostki i zaklecia.

## 13. Kryteria Ukonczenia

- Karta jednostki nadal dziala jak przed zmianami.
- Karta zaklecia `FriendlyUnit` moze byc przeciagana i zagrana na wlasna jednostke.
- Po wybraniu zaklecia `FriendlyUnit` podswietlaja sie tylko heksy z legalnymi wlasnymi jednostkami.
- Zaklecie bez celu moze zostac zagrane w fazie przygotowania bez wskazywania jednostki.
- Zaklecia nie da sie zagrac poza faza przygotowania.
- Nie ma per-frame alokacji dodanych do drag/hover.
- Testy edit mode dla logiki zaklec przechodza.
- Widok szczegolowy zaklecia nie pokazuje pol statystyk jednostki.
