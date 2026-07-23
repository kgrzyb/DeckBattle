# Plan: Deck Builder

## Cel

Dodać prosty Deck Builder dla MVP, który pozwala graczowi zbudować aktywny deck z kart posiadanych w `PlayerProfile`. Deck Builder ma korzystać z `CardCatalog` i zapisywać wynik w profilu gracza, ale nie powinien jeszcze zmieniać zasad bitwy poza przygotowaniem danych dla późniejszego startu meczu z wybranym deckiem.

## Zależności

Ten etap zakłada, że istnieją już:

- stabilne `CardId` w `CardDefinition`,
- `CardCatalog` jako źródło wszystkich kart MVP,
- `PlayerProfile` z kolekcją, aktywnym deckiem i shardami,
- `PlayerProfileStore` z walidacją oraz zapisem/odczytem.

Jeśli punkt 1 nie jest jeszcze zaimplementowany, Deck Builder należy ograniczyć do warstwy planowania albo użyć tymczasowego adaptera, który później zostanie zastąpiony przez `PlayerProfile`.

## Zakres

- Widok Deck Buildera w scenie `MainMenu`.
- Lista kart z kolekcji gracza.
- Lista kart w aktywnym decku.
- Dodawanie i usuwanie kart z decku.
- Limit rozmiaru decku zgodny z MVP.
- Brak duplikatów tej samej karty w decku.
- Zapis decku do profilu.
- Podstawowy feedback walidacji.
- Testy EditMode dla logiki wyboru decku.

Ten etap nie obejmuje jeszcze:

- pełnej kolekcji jako osobnego ekranu,
- filtrów i sortowania klasy produkcyjnej,
- startu bitwy z aktywnym deckiem,
- boostera,
- animacji UI wykraczających poza minimalny feedback.

## Proponowane klasy

### DeckBuilderService

Plain C# serwis odpowiedzialny za operacje na decku.

Proponowane API:

```csharp
DeckValidationResult ValidateDeck(PlayerProfile profile, CardCatalog catalog, DeckBuilderRules rules);
bool CanAddCard(PlayerProfile profile, string cardId, CardCatalog catalog, DeckBuilderRules rules, out DeckBuildFailReason reason);
bool TryAddCard(PlayerProfile profile, string cardId, CardCatalog catalog, DeckBuilderRules rules, out DeckBuildFailReason reason);
bool TryRemoveCard(PlayerProfile profile, string cardId);
```

Odpowiedzialność:

- sprawdzanie, czy karta jest w kolekcji,
- blokowanie duplikatów,
- blokowanie przekroczenia limitu,
- utrzymanie deterministycznej kolejności decku,
- zwracanie jasnego powodu błędu dla UI.

### DeckBuilderRules

Konfiguracja zasad decku.

Minimalne pola:

- `MinDeckSize`,
- `MaxDeckSize`.

Może być plain C# albo częścią `BattleConfig`/osobnego `ScriptableObject`. Dla MVP lepszy jest mały `ScriptableObject`, jeśli designer ma zmieniać limity w Inspectorze. Jeśli limity są stałe, plain C# będzie prostszy.

### DeckValidationResult

Struktura wyniku walidacji.

Minimalne pola:

- `IsValid`,
- `CardCount`,
- `MissingCardCount`,
- `DuplicateCardCount`,
- `DeckBuildFailReason Reason`.

### DeckBuildFailReason

Enum powodów błędu.

Przykłady:

- `None`,
- `UnknownCard`,
- `CardNotOwned`,
- `AlreadyInDeck`,
- `DeckFull`,
- `DeckTooSmall`,
- `DeckEmpty`.

### DeckBuilderController

MonoBehaviour dla widoku Deck Buildera w `MainMenu`.

Odpowiedzialność:

- odczyt profilu i katalogu,
- zbudowanie listy posiadanych kart,
- zbudowanie listy kart w decku,
- obsługa tapnięć przy dodawaniu/usuwaniu,
- zapis profilu po zmianie lub po kliknięciu `Save`,
- odświeżanie tylko wtedy, gdy dane się zmieniły.

Controller nie powinien implementować zasad decku. Zasady powinny zostać w `DeckBuilderService`, żeby dało się je testować bez sceny Unity.

### DeckBuilderCardItemView

Lekki komponent UI dla pojedynczej pozycji karty.

Wyświetla:

- nazwę karty,
- koszt AP,
- typ jednostki lub typ karty,
- podstawowe statystyki,
- stan: dostępna, w decku, zablokowana, wybrana.

Widok powinien mieć metodę typu:

```csharp
void Bind(CardDefinition card, bool isInDeck, bool canAdd);
```

Nie powinien samodzielnie pobierać profilu ani katalogu.

## UI i UX MVP

Deck Builder powinien być prosty i czytelny na telefonie w pionie.

Proponowany układ:

- górny pasek z tytułem, licznikiem decku i przyciskiem powrotu,
- sekcja aktywnego decku,
- sekcja posiadanych kart,
- przycisk zapisu lub automatyczny zapis po każdej zmianie.

Minimalny feedback:

- licznik `X / MaxDeckSize`,
- blokada dodawania, gdy deck jest pełny,
- oznaczenie kart już dodanych do decku,
- komunikat, gdy deck ma za mało kart.

Na MVP wystarczy tapnięcie:

- tap na karcie w kolekcji dodaje ją do decku,
- tap na karcie w decku usuwa ją z decku.

Drag and drop można zostawić poza zakresem, bo zwiększa koszt implementacji i ryzyko problemów dotykowych.

## Integracja z MainMenu

`MainMenuController` powinien przestać pokazywać placeholder dla Decku i zamiast tego otworzyć panel Deck Buildera.

Proponowany kierunek:

- zostawić `menuPanel`,
- dodać `deckBuilderPanel`,
- dodać `DeckBuilderController`,
- `ShowDeck()` aktywuje panel Deck Buildera i wywołuje jego refresh,
- `Back` wraca do menu oraz zapisuje profil, jeśli deck został zmieniony.

Nie mieszać w tym etapie logiki Collection View. Przycisk `Collection` może jeszcze zostać placeholderem albo używać prostego readonly widoku, jeśli będzie to potrzebne do testowania.

## Dane i zapis

Źródłem prawdy jest `PlayerProfile.ActiveDeckCardIds`.

Zasady:

- deck zawiera tylko `CardId`,
- UI mapuje `CardId` na `CardDefinition` przez `CardCatalog`,
- zapis wykonywać po zatwierdzeniu zmian albo po każdej zmianie, ale nie w `Update`,
- po zapisie ponownie walidować profil przez istniejącą walidację z punktu 1.

## Testy EditMode

Dodać testy dla `DeckBuilderService`:

- nie można dodać karty spoza katalogu,
- nie można dodać karty nieposiadanej,
- nie można dodać duplikatu,
- nie można przekroczyć `MaxDeckSize`,
- można usunąć kartę z decku,
- usunięcie nieistniejącej karty nie psuje decku,
- walidacja wykrywa za mały deck,
- walidacja akceptuje poprawny deck.

Testy UI można ograniczyć do minimum, jeśli logika pozostanie w plain C#.

## Kolejność implementacji

1. Dodać `DeckBuilderRules`, `DeckBuildFailReason` i `DeckValidationResult`.
2. Dodać `DeckBuilderService` z testami EditMode.
3. Dodać prefab lub komponent `DeckBuilderCardItemView`.
4. Dodać `DeckBuilderController`.
5. Rozszerzyć scenę `MainMenu` o panel Deck Buildera.
6. Podłączyć `MainMenuController.ShowDeck()` do nowego panelu.
7. Zweryfikować UI w widoku portretowym.
8. Uruchomić wąskie testy EditMode.

## Ryzyka i decyzje do sprawdzenia

- UI list kart może powodować kosztowne przebudowy layoutu, jeśli będzie odświeżane zbyt często.
- Przy większej liczbie kart trzeba będzie dodać pooling elementów listy, ale dla MVP z 8-12 kartami można zacząć prościej.
- Deck Builder nie powinien przechowywać referencji runtime z bitwy.
- `CardId` musi pozostać stabilne, bo zapis decku zależy od tych wartości.
- Trzeba zdecydować, czy zapis następuje automatycznie po zmianie, czy po kliknięciu `Save`.

## Definicja ukończenia

Etap jest gotowy, gdy:

- gracz może wejść z Main Menu do Deck Buildera,
- widzi posiadane karty i aktywny deck,
- może dodać oraz usunąć karty,
- system blokuje duplikaty i przekroczenie limitu,
- deck zapisuje się w `PlayerProfile`,
- testy EditMode dla logiki decku przechodzą,
- UI działa bez odświeżania w każdej klatce.
