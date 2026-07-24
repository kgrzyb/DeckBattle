# Plan: Jeden prefab karty

## Cel

Wszystkie miejsca pokazujące kartę mają korzystać z jednego wspólnego prefabu bazowego karty. Jeśli dany ekran potrzebuje dodatkowego zachowania, powinien opakować ten prefab w kontekstowy wrapper, a nie tworzyć osobny wygląd i osobne formatowanie karty.

Docelowo karta w ręce podczas bitwy, karta w Deck Builderze, podgląd/ghost przeciągania i przyszłe listy kolekcji powinny korzystać z tego samego komponentu prezentacyjnego oraz tej samej hierarchii UI dla frontu karty.

## Obecny stan

- `Assets/DeckBattle/Prefabs/Battle/PF_CardView.prefab` jest używany przez `BattleUIController` jako prefab kart w ręce.
- `Assets/DeckBattle/Prefabs/UI/PF_DeckBuilderCardItem.prefab` jest używany przez `DeckBuilderController` jako osobny prefab pozycji w deck builderze.
- `CardView` zawiera jednocześnie prezentację karty i zachowanie bitewne: tap, hold-to-drag, drag oraz komunikację z `BattleInputController`.
- `DeckBuilderCardItemView` duplikuje prezentację: nazwa, koszt, kolory stanów i formatowanie danych.
- `CardDetailsPopupView` ma osobny layout szczegółów i nie musi być zastępowany prefabem karty. Szczegóły/statystyki powinny pozostać w popupie albo innym dedykowanym widoku, nie na podstawowym widoku karty.

## Założenie architektoniczne

Wspólny prefab ma reprezentować kartę jako widok danych, bez wiedzy o tym, czy karta jest w bitwie, deck builderze, kolekcji albo popupie. Zachowania zależne od ekranu mają być w osobnych komponentach/wrapperach.

Proponowany podział:

- `CardFaceView` albo docelowo uproszczony `CardView`: komponent tylko do renderowania danych karty.
- `BattleHandCardView`: wrapper dla bitwy, obsługujący tap/drag i delegujący wygląd do wspólnego widoku.
- `DeckBuilderCardItemView`: wrapper deck buildera, obsługujący `Button`, dostępność, stan `inDeck/canAdd` i kliknięcie.
- `CardFaceView`: wspólny widok ograniczony do nazwy i kosztu AP, bez statystyk i typu.

Nazwa finalna może zostać dopasowana przy implementacji, ale ważne jest rozdzielenie `render card data` od `handle screen interaction`.

## Docelowe prefaby

### Wspólny prefab bazowy

Utworzyć jeden prefab bazowy, np.:

`Assets/DeckBattle/Prefabs/UI/PF_Card.prefab`

Powinien zawierać:

- tło/front karty,
- tekst nazwy,
- tekst kosztu AP,
- opcjonalny obraz grafiki karty z `CardDefinition.CardArt`,
- komponent wspólnego widoku, np. `CardFaceView`.

Prefab bazowy nie powinien zawierać:

- `BattleInputController`,
- logiki drag/tap,
- logiki dodawania/usuwania z decku,
- tekstów typu, opisu, HP, ATK, RNG, efektów spelli ani innych statystyk,
- bezpośrednich zależności od `BattleState`, `PlayerProfile` albo `DeckBuilderService`.

### Wrapper bitwy

`PF_CardView` może zostać zachowany jako prefab wrappera bitewnego dla stabilności referencji sceny `Battle.unity`, ale jego wnętrze powinno zawierać instancję/nested prefab `PF_Card`.

Wrapper powinien mieć komponent `BattleHandCardView` lub przebudowany `CardView`, który:

- trzyma referencję do wspólnego `CardFaceView`,
- binduje `CardRuntimeState`,
- obsługuje wyłącznie interakcje bitwy,
- ustawia stan wizualny wspólnego widoku: normalny, selected, dragging, disabled.

### Wrapper Deck Buildera

`PF_DeckBuilderCardItem` może zostać zachowany jako wrapper dla stabilności referencji sceny `MainMenu.unity`, ale jego wnętrze powinno zawierać nested prefab `PF_Card`.

Wrapper powinien:

- trzymać referencję do wspólnego `CardFaceView`,
- bindować `CardDefinition`,
- obsługiwać `Button` i callback `Action<string>`,
- przekazywać do wspólnego widoku stan: available, in deck, locked,
- nie posiadać własnych pól `nameText` i `costText`.

## Etapy wdrożenia

### 1. Wydzielenie wspólnego widoku danych karty

Utworzyć komponent prezentacyjny, np. `CardFaceView`.

Minimalne API:

```csharp
public void Bind(CardDefinition definition);
public void Bind(CardRuntimeState card);
public void SetVisualState(CardVisualState state);
public void Clear();
```

`Bind(CardRuntimeState)` powinno tylko wyciągać `Definition`, a nie podejmować decyzji gameplayowych ani sprawdzać typu karty.

`CardVisualState` powinien być małym enumem, np.:

```csharp
Normal,
Selected,
Dragging,
InDeck,
Locked,
Disabled
```

Jeśli paleta stanów będzie rosnąć, kolory lepiej trzymać w serializowanych polach widoku albo małym `ScriptableObject`, ale na ten etap wystarczą serializowane kolory w komponencie.

### 2. Ujednolicenie minimalnego formatowania tekstów

Przenieść formatowanie nazwy i kosztu z `CardView` i `DeckBuilderCardItemView` do jednego miejsca.

Zakres:

- nazwa: `CardDefinition.DisplayName`,
- koszt: `ApCost.ToString()` albo `"AP " + ApCost`, zależnie od layoutu prefabu.

Poza zakresem wspólnego widoku:

- typ karty,
- statystyki jednostki,
- efekt i amount spella,
- rarity,
- opis zasad działania.

Te dane powinny być pokazywane w `CardDetailsPopupView` albo innym kontekstowym panelu szczegółów.

Nie używać LINQ ani alokujących helperów w gorących ścieżkach. Bind kart ręki nie dzieje się co klatkę, ale nadal warto trzymać kod prosty i przewidywalny.

### 3. Przebudowa prefabów bez zmiany referencji scen

Najbezpieczniejsza ścieżka dla Unity:

1. Utworzyć `PF_Card.prefab` jako bazowy prefab wizualny.
2. W `PF_CardView.prefab` osadzić `PF_Card` i podpiąć referencję do wspólnego widoku.
3. W `PF_DeckBuilderCardItem.prefab` osadzić ten sam `PF_Card` i podpiąć referencję do wspólnego widoku.
4. Nie zmieniać nazw istniejących wrapper prefabów, dopóki sceny są do nich podpięte.
5. Dopiero po przejściu testów rozważyć rename komponentów, jeśli warto poprawić czytelność.

To ogranicza ryzyko utraty serializowanych referencji w `Battle.unity` i `MainMenu.unity`.

### 4. Migracja `CardView`

Obecny `CardView` powinien przestać sam ustawiać teksty i kolory konkretnych pól. Zamiast tego:

- zachować `CardRuntimeState Card`,
- zachować input bitewny,
- dodać `[SerializeField] private CardFaceView faceView;`,
- w `Bind(CardRuntimeState, BattleInputController)` wywołać `faceView.Bind(card)` i `faceView.SetVisualState(...)`,
- w `SetSelected` ustawić tylko stan wizualny,
- przy drag ustawiać stan `Dragging`.

W tym etapie można zachować nazwę `CardView`, żeby nie ruszać `BattleInputController` i testów szerzej niż trzeba.

### 5. Migracja `DeckBuilderCardItemView`

`DeckBuilderCardItemView` powinien zostać wrapperem.

Zmiany:

- usunąć serializowane pola tekstowe nazwy/kosztu i background z wrappera,
- dodać `[SerializeField] private CardFaceView faceView;`,
- w `Bind(CardDefinition card, bool isInDeck, bool canAdd, Action<string> onClicked)` wywołać `faceView.Bind(card)`,
- wybrać `CardVisualState.InDeck`, `Normal` albo `Locked`,
- zostawić tylko `Button`, `cardId` i callback kliknięcia.

Warto zachować obecne API `Bind`, żeby `DeckBuilderController` nie wymagał dużych zmian.

### 6. Opcjonalne użycie wspólnego prefabu dla ghosta

Obecny ghost w `BattleUIController` ma osobne pola `ghostNameText` i `ghostCostText`. Po podstawowej migracji można uprościć to do pooled/stałej instancji wspólnego prefabu.

Proponowane podejście:

- dodać `CardFaceView ghostCardFaceView`,
- w `ShowCardGhost` używać `ghostCardFaceView.Bind(card)` i `SetVisualState(Dragging)`,
- usunąć osobne teksty ghosta dopiero po zweryfikowaniu prefabów.

Ten krok jest mniej pilny niż ręka i deck builder, ale domyka zasadę jednego wyglądu karty.

### 7. Testy i weryfikacja

EditMode:

- dodać test formatowania/widoku, jeśli `CardFaceView` ma testowalne metody bez zależności od sceny,
- dodać test, że `DeckBuilderCardItemView.Bind` ustawia callback i stan interakcji, ale nie duplikuje danych,
- zachować istniejące testy `DeckBuilderService`, `DeckHandUnitPlayTests` i `CardDetailsPopupViewTests`.

Manualna weryfikacja w Unity Editor:

- scena `Battle`: karta w ręce pokazuje nazwę i koszt AP,
- tap karty pokazuje szczegóły,
- hold/drag dalej działa i zmienia kolor/stan,
- zagranie jednostki i spella działa jak przed zmianą,
- scena `MainMenu`: deck builder pokazuje kolekcję i deck z kartami zawierającymi nazwę i koszt AP,
- dodawanie/usuwanie kart działa,
- stany `in deck`, `available`, `locked` są wizualnie rozróżnialne,
- layout skaluje się na typowych proporcjach telefonu.

Jeśli Unity Editor jest już otwarty, testy uruchomić z menu:

`DeckBattle > Tests > Run EditMode Tests`

Nie uruchamiać EditMode testów w batchmode, jeśli ten sam projekt jest otwarty w Editorze.

## Ryzyka

- Zmiana skryptu podpiętego do prefabów może zerwać serializowane referencje w scenach. Dlatego lepiej najpierw dodać wspólny komponent i wrappery, a nie od razu masowo rename'ować klasy.
- Jeśli `PF_Card` zostanie zagnieżdżony w wrapperach, override'y prefabów trzeba sprawdzić w Inspectorze, bo Unity może zachować lokalne różnice rozmiaru/kolorów.
- Wspólny prefab nie powinien wymuszać jednego rozmiaru dla wszystkich kontekstów. Rozmiar powinien pochodzić z wrappera/layoutu rodzica, a bazowy prefab powinien dobrze działać w różnych `RectTransform`.
- Nie należy przenosić całego popupu szczegółów do wspólnego prefabu. Popup ma inną funkcję i powinien pozostać miejscem dla statystyk, typu, opisu i szczegółów działania karty.

## Kryteria akceptacji

- Istnieje jeden prefab bazowy karty używany przez wrapper bitwy i wrapper deck buildera.
- `CardView` nie duplikuje już pól tekstowych nazwy/kosztu i kolorów, które należą do wspólnego wyglądu karty.
- `DeckBuilderCardItemView` nie posiada własnej prezentacji nazwy/kosztu karty, tylko używa wspólnego widoku.
- Podstawowy widok karty nie zawiera pozycji statystyk, typu ani opisu; pokazuje tylko nazwę i koszt AP.
- `Battle.unity` i `MainMenu.unity` nadal mają poprawne referencje prefabów.
- Zachowanie bitewne kart pozostaje bez regresji: tap, selection, drag, ghost i play.
- Deck Builder nadal pozwala dodawać i usuwać karty oraz pokazuje poprawne stany.
- Zmiana nie dodaje nowych paczek ani ciężkich zależności runtime.
- Nie pojawiają się nowe aktualizacje UI co klatkę poza istniejącym zachowaniem hold-to-drag w kartach ręki.
