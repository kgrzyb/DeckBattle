# Plan: Save/load profilu gracza

## Cel

Dodać lub domknąć prosty zapis profilu gracza przez `PlayerProfileStore`, tak aby kolekcja kart, aktywny deck i podstawowe waluty/progres były zapisywane jako czytelny JSON w `Application.persistentDataPath`.

MVP ma być małe, debugowalne i niezależne od `PlayerPrefs`. `PlayerPrefs` nie powinien przechowywać list kart, decków ani kolekcji, bo utrudni to debugowanie, migracje i ręczne sprawdzanie save'a.

## Aktualny stan do sprawdzenia

W repo istnieją już:

- `Assets/DeckBattle/Scripts/Profile/PlayerProfile.cs`,
- `Assets/DeckBattle/Scripts/Profile/PlayerProfileStore.cs`,
- `Assets/DeckBattle/Tests/EditMode/PlayerProfileStoreTests.cs`,
- `CardCatalog` z metodami do kolekcji startowej i domyślnego decku.

Pierwszym krokiem implementacji powinno być porównanie istniejącego kodu z wymaganiami poniżej. Jeśli obecna implementacja już spełnia wymaganie, nie przepisywać jej bez potrzeby.

## Zakres MVP

- Zapisywać profil jako JSON w `Application.persistentDataPath`.
- Udostępnić API:

```csharp
PlayerProfile LoadOrCreateDefault(CardCatalog catalog);
void Save(PlayerProfile profile);
```

- Opcjonalnie dodać:

```csharp
void ResetProfile();
```

`ResetProfile()` traktować jako narzędzie debug/dev. Nie podłączać do produkcyjnego UI bez jawnej decyzji.

## Model danych

`PlayerProfile` powinien pozostać prostym serializowalnym modelem danych, bez zależności od UI i bez referencji do `ScriptableObject`.

Minimalne pola:

- `SaveVersion`,
- `UnlockedCardIds`,
- `ActiveDeckCardIds`,
- `Shards` lub inna podstawowa waluta MVP, jeśli już jest używana.

Karty zapisywać po stabilnym `CardId`, a nie przez referencje do assetów. Po wczytaniu mapowanie `CardId -> CardDefinition` powinno odbywać się przez `CardCatalog`.

## PlayerProfileStore

Proponowana odpowiedzialność:

- buduje domyślną ścieżkę: `Path.Combine(Application.persistentDataPath, "player-profile.json")`,
- pozwala podać ścieżkę w konstruktorze na potrzeby testów EditMode,
- tworzy katalog zapisu przed `Save`,
- czyta i zapisuje JSON przez `JsonUtility`,
- przy braku pliku tworzy profil domyślny z `CardCatalog`,
- przy pustym/uszkodzonym JSON tworzy profil domyślny i loguje ostrzeżenie,
- waliduje profil po wczytaniu.

Nie dodawać ciężkich zależności JSON, jeśli `JsonUtility` wystarcza dla obecnego modelu.

## Domyślny profil

`LoadOrCreateDefault(catalog)` powinien utworzyć profil z:

- `SaveVersion = PlayerProfile.CurrentSaveVersion`,
- startową kolekcją z `catalog.GetStartingCollectionCardIds(...)`,
- aktywnym deckiem z `catalog.GetDefaultDeckCardIds(...)`,
- zerową walutą/progresem, jeśli brak innych wymagań.

Jeśli katalog nie ma osobnej kolekcji startowej lub domyślnego decku, fallback powinien być jawny i przewidywalny, np. użycie kart z głównej listy katalogu.

## Walidacja po wczytaniu

Po deserializacji profil powinien zostać znormalizowany:

- brakujące listy zamienić na puste listy,
- usunąć puste `CardId`,
- przyciąć whitespace z `CardId`,
- usunąć duplikaty,
- usunąć karty spoza `CardCatalog`,
- usunąć z aktywnego decku karty, których gracz nie ma w kolekcji,
- ujemną walutę sprowadzić do zera,
- podbić `SaveVersion` do aktualnej wersji MVP.

Jeśli aktywny deck po walidacji jest pusty, odtworzyć domyślny deck z dostępnych kart.

Decyzja do podjęcia: czy `LoadOrCreateDefault` ma automatycznie zapisać naprawiony profil po walidacji. Dla MVP rekomendacja: nie zapisywać automatycznie w pierwszej wersji, chyba że wywołujący jawnie robi `Save(profile)` po zmianach. To zmniejsza ryzyko nadpisania save'a podczas debugowania.

## Integracja

Miejsca użycia powinny dostać `PlayerProfile` jako dane, a nie znać szczegółów pliku.

Priorytet integracji:

1. `MainMenuController` ładuje profil przy starcie menu lub przed wejściem do Deck Buildera.
2. Deck Builder modyfikuje `ActiveDeckCardIds` i wywołuje `Save(profile)` tylko po realnej zmianie.
3. Start bitwy używa aktywnego decku z profilu przez osobny builder danych startowych, bez ładowania pliku w `BattleController`.
4. Ewentualne nagrody po bitwie aktualizują profil i zapisują go raz po zakończeniu ekranu wyniku.

Nie zapisywać profilu co klatkę ani przy każdym odświeżeniu UI.

## Testy EditMode

Dodać lub utrzymać testy:

- `LoadOrCreateDefault` tworzy profil, gdy plik nie istnieje,
- `Save` i `LoadOrCreateDefault` robią round-trip JSON,
- uszkodzony lub pusty plik daje profil domyślny,
- walidacja usuwa duplikaty i brakujące karty,
- aktywny deck nie może zawierać kart spoza kolekcji gracza,
- pusty/niepoprawny deck dostaje fallback,
- `ResetProfile` usuwa plik, jeśli metoda zostaje włączona.

Testy powinny używać tymczasowej ścieżki przekazanej do konstruktora store'a, a nie prawdziwego `Application.persistentDataPath`.

## Kolejność implementacji

1. Porównać obecny `PlayerProfileStore` z zakresem MVP.
2. Uzupełnić brakujące testy dla pustego/uszkodzonego JSON i `ResetProfile`, jeśli brakuje pokrycia.
3. Upewnić się, że domyślny konstruktor używa `Application.persistentDataPath`.
4. Upewnić się, że konstruktor testowy przyjmuje pełną ścieżkę pliku.
5. Zweryfikować walidację profilu względem `CardCatalog`.
6. Podłączyć store do przepływu menu/deck buildera tylko w miejscach, gdzie profil faktycznie jest ładowany lub zmieniany.
7. Uruchomić wąskie testy EditMode dla `PlayerProfileStore`.

## Ryzyka

- Automatyczne naprawianie i zapisywanie save'a może utrudnić debugowanie uszkodzonych plików.
- Zmiana `CardId` w assetach po publikacji unieważni stare save'y; `CardId` musi być traktowane jako stabilny identyfikator.
- `JsonUtility` nie obsługuje wszystkich struktur C#, więc model profilu powinien pozostać prosty.
- Operacje plikowe są tanie w skali MVP, ale nadal powinny być wykonywane poza hot path UI/gameplay.

## Definicja ukończenia

Etap jest gotowy, gdy:

- profil zapisuje się jako czytelny `player-profile.json` w `Application.persistentDataPath`,
- `LoadOrCreateDefault(CardCatalog catalog)` działa dla braku pliku, poprawnego pliku i uszkodzonego pliku,
- `Save(PlayerProfile profile)` tworzy katalog i zapisuje aktualny profil,
- listy kart nie używają `PlayerPrefs`,
- istnieją testy EditMode pokrywające zapis, odczyt i walidację,
- integracja nie dodaje zapisu/odczytu w pętlach klatkowych.
