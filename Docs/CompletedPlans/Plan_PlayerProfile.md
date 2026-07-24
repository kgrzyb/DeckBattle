# Plan: PlayerProfile i kolekcja gracza

## Cel

Dodać minimalną, trwałą warstwę profilu gracza dla MVP. Profil ma przechowywać odblokowane karty, aktywny deck oraz liczbę shardów, tak aby kolejne etapy mogły zbudować realny Collection View, Deck Builder i Booster Result bez przepisywania logiki bitwy.

## Zakres pierwszego etapu

- Stabilne ID kart w danych.
- Katalog wszystkich kart dostępnych w MVP.
- Model profilu gracza.
- Lokalny zapis i odczyt profilu.
- Walidacja profilu po odczycie.
- Testy EditMode dla logiki profilu.

Ten etap nie obejmuje jeszcze UI kolekcji, UI deck buildera, boostera ani zmian w pętli nagród po zwycięstwie.

## Proponowane klasy i assety

### CardDefinition

Dodać stabilny identyfikator karty, np. `Id`.

Założenia:

- `Id` jest niepustym stringiem.
- `Id` jest stabilny między buildami i zapisami.
- Save nie powinien opierać się na nazwie assetu ani runtime ID.

### CardCatalog

Nowy `ScriptableObject` zawierający listę wszystkich kart dostępnych w MVP.

Odpowiedzialność:

- mapowanie `Id -> CardDefinition`,
- walidacja brakujących i zdublowanych ID,
- udostępnienie startowej puli kart dla domyślnego profilu.

### PlayerProfile

Plain C# model danych profilu.

Minimalne pola:

- lista odblokowanych `CardId`,
- lista `CardId` w aktywnym decku,
- liczba shardów,
- wersja zapisu, jeśli będzie potrzebna do migracji.

### PlayerProfileStore

Serwis zapisu i odczytu profilu.

Proponowane API:

```csharp
PlayerProfile LoadOrCreateDefault(CardCatalog catalog);
void Save(PlayerProfile profile);
void ResetProfile();
```

MVP może używać JSON w `Application.persistentDataPath`. Nie używać `PlayerPrefs` do list kart, bo zapis będzie mniej czytelny i trudniejszy do walidacji.

## Domyślny profil

Przy pierwszym uruchomieniu utworzyć profil z:

- startową kolekcją kart MVP,
- domyślnym deckiem mieszczącym się w limitach MVP,
- `shards = 0`.

Na potrzeby szybkiego testowania można na początku odblokować wszystkie obecne jednostki MVP. Później można zawęzić startową kolekcję, jeśli wymaga tego balans.

## Walidacja po odczycie

Po załadowaniu profilu należy:

- usunąć ID kart, których nie ma w `CardCatalog`,
- usunąć duplikaty z kolekcji,
- usunąć duplikaty z decku,
- usunąć z decku karty, których gracz nie posiada,
- odtworzyć domyślny deck, jeśli zapisany deck jest pusty lub niegrywalny.

Walidacja ma chronić przed uszkodzonym zapisem i zmianami w assetach między wersjami.

## Testy EditMode

Dodać testy obejmujące:

- utworzenie domyślnego profilu,
- zapis i odczyt profilu,
- usuwanie duplikatów z kolekcji i decku,
- usuwanie kart spoza katalogu,
- odrzucanie kart z decku, których gracz nie posiada,
- fallback do domyślnego decku, gdy zapisany deck jest pusty lub niepoprawny.

## Kolejność implementacji

1. Dodać `Id` do `CardDefinition` i uzupełnić istniejące assety kart.
2. Dodać `CardCatalog` i utworzyć asset katalogu dla obecnych kart MVP.
3. Dodać `PlayerProfile`.
4. Dodać `PlayerProfileStore` z JSON save/load.
5. Dodać walidację profilu.
6. Dodać testy EditMode.
7. Dopiero w następnym etapie podłączyć profil do `MainMenuController`, Collection View i Deck Buildera.

## Ryzyka i decyzje do sprawdzenia

- Trzeba zachować stabilność `Id` po zmianach nazw assetów.
- Katalog kart powinien być jednym źródłem prawdy dla metagry i bitwy.
- Zapis profilu nie powinien ładować ciężkich assetów w gorących ścieżkach gry.
- Walidacja powinna być deterministyczna i bez alokacji w runtime pętli bitwy.
- Ten etap nie powinien zmieniać zachowania istniejącej sceny bitwy poza przyszłą możliwością podania decku z profilu.

## Definicja ukończenia

Etap jest gotowy, gdy:

- profil można utworzyć, zapisać i odczytać,
- profil przechowuje kolekcję, deck i shardy,
- niepoprawny zapis jest naprawiany przez walidację,
- testy EditMode dla profilu przechodzą,
- istniejące systemy bitwy pozostają bez regresji.
