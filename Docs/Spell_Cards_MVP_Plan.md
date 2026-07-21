# Deck Battle - Spell Cards MVP Plan

## 1. Cel

Przygotowac architekture kart pod przyszle `SpellCard`, bez budowania duzego systemu zaklec.

Pierwszy etap ma rozdzielic:

- definicje kart,
- runtime state karty,
- walidacje zagrania,
- wykonanie efektu.

Zakres MVP powinien pozwolic dodac 1-2 proste zaklecia testowe pozniej, np. tymczasowy buff ataku na najblizsza walke, ale bez statusow, trapow, triggerow, stackow efektow ani rozbudowanego frameworka.

Wszystkie zaklecia sa zagrywane tylko w fazie przygotowania. Poniewaz w tej fazie jednostki przeciwnika sa ukryte, MVP nie powinno dodawac zaklec wymagajacych targetowania enemy unit, np. direct damage w konkretna jednostke przeciwnika.

## 2. Aktualny Stan

Obecnie karta w runtime jest praktycznie karta jednostki:

- `CardRuntimeState` trzyma `UnitDefinition Definition`.
- `DeckService.CreateDeck` przyjmuje `IList<UnitDefinition>`.
- `BattleState.Create` przyjmuje talie `IList<UnitDefinition>`.
- `BattleController` serializuje `List<UnitDefinition>` jako talie gracza i AI.
- `UnitPlayService` laczy walidacje kosztu, obecnosci w rece, placement na planszy i stworzenie `RuntimeUnit`.
- `BattleInputController` oraz `BoardPresenter.HighlightPlayableTiles` zakladaja, ze kazda karta wymaga legalnego heksa deploymentu.
- `CardView` i `CardDetailsPopupView` wyswietlaja tylko statystyki jednostki.

To jest dobry punkt startu dla jednostek, ale spell cards wymagaja wspolnej definicji karty wyzej niz `UnitDefinition`.

## 3. Docelowy Minimalny Model

### CardDefinition

Dodac bazowy typ danych karty, najlepiej jako `ScriptableObject`:

```text
CardDefinition
  - CardId
  - DisplayName
  - Rarity
  - ApCost
  - CardArt
  - CardKind
```

`CardKind` powinien na MVP miec tylko:

```text
Unit
Spell
```

### UnitCard

Istniejacy `UnitDefinition` powinien stac sie karta jednostki przez dziedziczenie po `CardDefinition`.

Najbezpieczniejsza nazwa w pierwszym kroku:

```text
UnitDefinition : CardDefinition
```

Nie robic od razu rename do `UnitCardDefinition`, bo Unity assety `.asset` i skrypty w prefabach moga zalezec od istniejacej klasy. Jezeli nazwa `UnitCard` jest pozniej wymagana, zrobic to jako osobna migracje z kontrola `.meta` i asset references.

### SpellCard

Dodac osobny typ definicji:

```text
SpellDefinition : CardDefinition
  - SpellEffectKind
  - TargetingKind
  - Amount
```

Na MVP wystarcza enumy:

```text
SpellEffectKind
  - BuffAttackNextCombat

SpellTargetingKind
  - FriendlyUnit
```

Nie dodawac jeszcze list efektow, polymorficznych effect assets, statusow, triggerow ani trapow.

### CardRuntimeState

Zmienic runtime state na wspolny typ:

```text
CardRuntimeState
  - RuntimeCardId
  - CardDefinition Definition
  - CardLocation Location
```

Dla wygody mozna dodac bezalokacyjne property:

```text
UnitDefinition UnitDefinition => Definition as UnitDefinition
SpellDefinition SpellDefinition => Definition as SpellDefinition
```

Nie trzymac mutable statystyk jednostki w `CardRuntimeState`. Runtime jednostki nadal powinien byc w `RuntimeUnit` / `UnitRuntimeState`.

## 4. Walidacja I Wykonanie

Zachowac obecny wzorzec z `UnitPlayService`:

```text
Validate -> Result/FailReason -> Execute
```

### UnitPlayService

Zostawic jako serwis tylko dla jednostek:

- odrzuca karte, jezeli `card.Definition` nie jest `UnitDefinition`,
- waliduje faze preparation,
- waliduje hand/AP,
- waliduje slot deploymentu,
- waliduje heks,
- tworzy `RuntimeUnit`.

### SpellPlayService

Dodac pozniej jako rownolegly serwis:

```text
SpellPlayService
  - ValidatePlay(BattleState, PlayerBattleState, CardRuntimeState, SpellTarget)
  - PlaySpell(BattleState, PlayerBattleState, CardRuntimeState, SpellTarget)
```

`SpellTarget` powinien byc prostym readonly structem, np.:

```text
SpellTarget
  - bool HasCoord
  - HexCoord Coord
  - RuntimeUnit Unit
```

Na MVP wystarczy wybrac jeden sposob targetowania per spell:

- buff attack next combat: target friendly unit.

Nie robic jeszcze targetowania obszarowego, targetowania gracza, global effects ani target conditions jako osobnych assetow.

## 5. Punkty Integracji

### DeckService / BattleState

Zmienic talie z `IList<UnitDefinition>` na `IList<CardDefinition>`.

Najmniejszy zakres:

- `DeckService.CreateDeck(IList<CardDefinition> definitions, ...)`
- `BattleState.Create(... IList<CardDefinition> playerDeck, IList<CardDefinition> enemyDeck, ...)`
- `BattleController` serialized deck fields jako `List<CardDefinition>`.

Istniejace `UnitDefinition` assety nadal beda pasowac, jezeli dziedzicza po `CardDefinition`.

### HandService

`CanPayForCard` powinno czytac `card.Definition.ApCost` z bazowej definicji.

Nie dodawac specjalnych kosztow zaklec w pierwszym kroku.

### Input

`BattleInputController` powinien rozgaleziac zachowanie po typie karty:

- `Unit`: obecny drag/tap na deployment tile.
- `Spell`: osobny tryb wyboru celu.

Na etapie samej architektury mozna tylko wydzielic dispatch:

```text
if card is Unit -> UnitPlayService
if card is Spell -> SpellPlayService
```

Nie trzeba jeszcze implementowac pelnego spell UX, dopoki nie ma testowych zaklec.

### BoardPresenter

Obecne `HighlightPlayableTiles` jest unit-specific. Docelowo:

- `HighlightUnitPlayableTiles(...)`
- pozniej `HighlightSpellTargets(...)`

Nie mieszac reguly deploymentu jednostek z targetowaniem zaklec.

### UI

`CardView` powinien wyswietlac dane z `CardDefinition`:

- nazwa,
- AP,
- typ karty,
- prosty opis/stats line.

`CardDetailsPopupView` powinien miec rozgalezienie:

- unit details dla `UnitDefinition`,
- spell details dla `SpellDefinition`.

Na MVP nie trzeba budowac nowego layoutu popupu. Wystarczy bezpiecznie ukrywac pola statystyk jednostki dla spell card.

### Enemy AI

Na poczatku AI moze ignorowac spells i dalej grac tylko jednostki.

Warunek:

- `EnemyPreparationAI.TryFindPlay` filtruje tylko `UnitDefinition`,
- spell cards w rece AI nie blokuja decyzji ready/countdown.

Pozniejsze spell AI powinno byc osobnym etapem.

## 6. Proponowana Kolejnosc Pracy

### Etap 1 - Wspolna Definicja Karty

- Dodac `CardDefinition`.
- Dodac `CardKind`.
- Przeniesc wspolne pola z `UnitDefinition` do `CardDefinition`.
- Zmienic `UnitDefinition` na dziedziczenie po `CardDefinition`.
- Zachowac kompatybilnosc istniejacych unit assetow.

Kryterium konca:

- istniejace unit cards nadal dzialaja bez zmian gameplayu.

### Etap 2 - Wspolny Runtime Card State

- Zmienic `CardRuntimeState.Definition` na `CardDefinition`.
- Zaktualizowac `DeckService`, `BattleState.Create`, `BattleController` i test helpers.
- Dostosowac miejsca, ktore potrzebuja unit stats, do jawnego castu na `UnitDefinition`.

Kryterium konca:

- wszystkie obecne testy unit/deck/hand przechodza.
- nie ma jeszcze spell gameplayu.

### Etap 3 - Oddzielenie Unit Play Od Ogolnego Zagrywania Kart

- Dodac maly dispatch serwis, np. `CardPlayService`, albo zostawic dispatch w `BattleController`.
- `TryPlayPlayerCard` powinno umiec odrzucic nieobslugiwany typ karty bez crasha.
- `BoardPresenter.HighlightPlayableTiles` przemianowac logicznie na unit-only albo dodac wrapper wybierajacy typ highlightu.

Kryterium konca:

- unit placement zachowuje sie tak samo,
- kod nie zaklada juz, ze kazda karta jest jednostka.

### Etap 4 - Minimalny SpellDefinition Bez Efektow

- Dodac `SpellDefinition`.
- Dodac enumy `SpellEffectKind` i `SpellTargetingKind`.
- Dodac `OnValidate` dla kosztu i amount.
- Dodac test helper `CreateSpell`.

Kryterium konca:

- spell asset moze istniec w projekcie i talii,
- nie musi byc jeszcze grywalny.

### Etap 5 - Minimalny SpellPlayService

Dopiero po etapach 1-4:

- dodac `SpellPlayService.ValidatePlay`,
- dodac `PlaySpellResult` i `PlaySpellFailReason`,
- obsluzyc tylko 1 efekt MVP.

Rekomendowane pierwsze zaklecie:

```text
BuffAttackNextCombat
  - target: friendly unit
  - effect: jednorazowy bonus do Attack w najblizszej walce
```

To zaklecie wymaga dodatkowego runtime pola na jednostce albo tuningu walki, ale pasuje do ograniczenia, ze zaklecia sa zagrywane tylko w fazie przygotowania i nie moga targetowac ukrytych jednostek przeciwnika.

## 7. Testy

Minimalne testy edit mode:

- `DeckService` tworzy deck z `CardDefinition` i zachowuje runtime IDs.
- `UnitPlayService` odrzuca spell card jako zly typ.
- `UnitPlayService` dalej zagrywa unit card bez regresji.
- `HandService.CanPayForCard` dziala dla bazowej definicji karty.
- `PreparationTurnService.CanPlayAnyUnit` ignoruje spell cards, jezeli szuka tylko jednostek.
- Po dodaniu `SpellPlayService`: spell odrzuca zla faze, brak AP, brak karty w rece i zly target.

Nie uruchamiac play mode testow tylko dla refactoru definicji kart, chyba ze zmieniany jest input albo scene binding.

## 8. Ryzyka

### Unity Asset Compatibility

Zmiana klasy/nazwy `UnitDefinition` moze naruszyc asset references. Dlatego pierwszy krok powinien zachowac nazwe `UnitDefinition` i tylko dodac bazowe dziedziczenie.

### Zbyt Szybki Effect Framework

Najwieksze ryzyko projektowe to przedwczesne dodanie abstrakcyjnych effect assets, triggerow, statusow i warunkow. Na MVP wystarcza enum + amount + target kind.

### Input I Highlighting

Spell targetowanie nie powinno korzystac z reguly deployment tile. Trzeba rozdzielic highlighty jednostek od highlightow spell targetow, inaczej UI bedzie mylacy.

### Mobile Performance

Walidacja targetow nie powinna alokowac list ani skanowac niepotrzebnie calego modelu w kazdej klatce. Przy drag/hover uzyc istniejacych kolekcji i prostych petli, podobnie jak obecny `BoardPresenter`.

## 9. Poza Zakresem MVP

- status effects,
- traps/secrets,
- aura effects,
- delayed effects,
- chain/stack resolution,
- effect assets jako osobne ScriptableObjecty,
- targetowanie obszarowe,
- targetowanie wielu celow,
- spell AI,
- animacje VFX zaklec,
- deck builder filtering po typach kart.

## 10. Rekomendacja

Najpierw zrobic tylko etapy 1-3 jako refactor bez zmiany zachowania. Dopiero kiedy unit cards nadal dzialaja i testy sa zielone, dodac `SpellDefinition` jako pasywny asset. Pierwszy prawdziwy spell powinien byc `BuffAttackNextCombat`, bo nie wymaga widocznych jednostek przeciwnika w fazie przygotowania.
