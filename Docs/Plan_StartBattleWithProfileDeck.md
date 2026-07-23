# Plan: Start Battle z aktywnym deckiem

## Cel

Podłączyć aktywny deck zapisany w `PlayerProfile` do startu sceny `Battle`, tak aby gracz rozpoczynał mecz kartami wybranymi w Deck Builderze. Ten etap ma zastąpić testowy lub statyczny deck danymi z profilu, ale nie powinien zmieniać zasad walki ani struktury rund.

## Zależności

Ten etap zakłada, że istnieją już:

- stabilne `CardId` w `CardDefinition`,
- `CardCatalog`,
- `PlayerProfile` i `PlayerProfileStore`,
- walidacja profilu,
- Deck Builder zapisujący aktywny deck w profilu.

Jeśli Deck Builder nie jest jeszcze gotowy, można użyć aktywnego decku z domyślnego profilu.

## Zakres

- Przygotowanie danych startowych meczu z profilu gracza.
- Mapowanie `CardId` z aktywnego decku na `CardDefinition`.
- Przekazanie decku z `MainMenu` do sceny `Battle`.
- Fallback na deck domyślny, jeśli zapis jest pusty lub niepoprawny.
- Zachowanie istniejącego decku AI lub przygotowanie prostego decku AI z katalogu.
- Testy EditMode dla budowania danych startowych.

Ten etap nie obejmuje:

- boostera po zwycięstwie,
- matchmakingu,
- wyboru przeciwnika,
- rozbudowanego AI deck buildingu,
- zmian w zasadach dobierania kart, AP, rund lub walki.

## Proponowane klasy

### BattleStartData

Plain C# lub prosty runtime model danych wejściowych dla sceny bitwy.

Minimalne pola:

- `IReadOnlyList<CardDefinition> PlayerDeck`,
- `IReadOnlyList<CardDefinition> EnemyDeck`,
- `int Seed`.

Opcjonalnie później:

- identyfikator przeciwnika,
- poziom trudności,
- nagroda po zwycięstwie.

### BattleStartDataBuilder

Serwis budujący `BattleStartData` z `PlayerProfile` i `CardCatalog`.

Proponowane API:

```csharp
BattleStartData Build(PlayerProfile profile, CardCatalog catalog, BattleStartRules rules, int seed);
```

Odpowiedzialność:

- walidacja aktywnego decku,
- mapowanie `CardId` na `CardDefinition`,
- fallback do domyślnego decku,
- wybór decku AI,
- zachowanie deterministycznej kolejności kart.

### BattleStartRules

Konfiguracja zasad startu meczu.

Minimalne pola:

- minimalny rozmiar decku,
- maksymalny rozmiar decku,
- domyślny seed lub tryb generowania seed,
- tryb wyboru decku AI.

Dla MVP może korzystać z tych samych limitów co `DeckBuilderRules`, jeśli nie ma powodu rozdzielać konfiguracji.

### BattleSession

Mały obiekt lub statyczny holder do przekazania danych między scenami.

Opcja MVP:

```csharp
public static class BattleSession
{
    public static BattleStartData PendingStartData;
}
```

To jest proste, ale trzeba pilnować czyszczenia po starcie bitwy. Później można zastąpić to trwalszym systemem sesji lub bootstrapem scen.

Alternatywa:

- `DontDestroyOnLoad` bootstrap object z `BattleStartData`.

Na MVP statyczny holder jest akceptowalny, jeśli jest mały, jawny i czyszczony po użyciu.

## Integracja z MainMenu

`MainMenuController.StartBattle()` powinien:

1. Załadować lub użyć aktualnego `PlayerProfile`.
2. Zwalidować aktywny deck.
3. Zbudować `BattleStartData`.
4. Zapisać dane w `BattleSession`.
5. Załadować scenę `Battle`.

Jeśli deck jest niepoprawny:

- użyć domyślnego decku z profilu/katalogu, albo
- zablokować start i pokazać komunikat w menu.

Rekomendacja MVP: jeśli Deck Builder już istnieje, blokować start przy niepoprawnym decku. Jeśli Deck Builder jeszcze nie jest gotowy, używać fallbacku, żeby utrzymać możliwość testowania bitwy.

## Integracja z BattleController

`BattleController` powinien przy inicjalizacji:

1. Sprawdzić, czy `BattleSession.PendingStartData` istnieje.
2. Jeśli istnieje, użyć `PlayerDeck`, `EnemyDeck` i `Seed`.
3. Wyczyścić `BattleSession.PendingStartData`.
4. Jeśli danych nie ma, użyć istniejącego fallbacku z Inspector/configu.

To pozwala nadal odpalać scenę `Battle` bezpośrednio w Editorze.

## Deck AI

Na MVP można zostawić prosty deck AI.

Opcje:

- użyć osobnej listy kart AI z `BattleController` lub `BattleConfig`,
- użyć domyślnego decku z `CardCatalog`,
- skopiować deck gracza jako tymczasowego przeciwnika testowego.

Rekomendacja: osobny domyślny deck AI z katalogu albo obecna lista z `BattleController`, jeśli już istnieje. Kopiowanie decku gracza jest dobre tylko jako fallback developerski.

## Fallbacki

Scena `Battle` musi działać także bez wejścia przez `MainMenu`.

Fallback powinien obsłużyć:

- brak `BattleSession`,
- pusty deck gracza,
- brakujące `CardId`,
- brak katalogu,
- niepełny profil.

Fallback nie powinien maskować błędów w buildzie produkcyjnym bez logu. W Editorze warto logować ostrzeżenie z jasną przyczyną.

## Testy EditMode

Dodać testy dla `BattleStartDataBuilder`:

- buduje deck gracza z aktywnego decku profilu,
- zachowuje kolejność kart z profilu,
- ignoruje lub odrzuca `CardId` spoza katalogu zgodnie z przyjętą walidacją,
- używa fallbacku, gdy deck jest pusty,
- buduje deck AI,
- zwraca stabilny seed, jeśli seed jest podany,
- nie zwraca duplikatów, jeśli zasady decku ich zabraniają.

Jeśli `BattleSession` będzie statyczny, dodać test czyszczenia danych po użyciu albo przynajmniej pokryć to testem `BattleController`/adaptera bez ładowania sceny.

## Kolejność implementacji

1. Sprawdzić obecne źródło decków w `BattleController`.
2. Dodać `BattleStartData`.
3. Dodać `BattleStartDataBuilder` i testy EditMode.
4. Dodać `BattleSession` lub mały bootstrap do przekazania danych między scenami.
5. Zmienić `MainMenuController.StartBattle()` tak, aby budował dane startowe.
6. Zmienić inicjalizację `BattleController`, aby preferowała `BattleSession`, ale zachowała fallback z Inspectora.
7. Zweryfikować bezpośrednie uruchomienie sceny `Battle` w Editorze.
8. Uruchomić wąskie testy EditMode.

## Ryzyka i decyzje do sprawdzenia

- Statyczny `BattleSession` trzeba czyścić po użyciu, żeby kolejny test lub powrót do menu nie użył starych danych.
- Nie można uzależnić sceny `Battle` wyłącznie od `MainMenu`, bo utrudni to testowanie w Editorze.
- `BattleController` nie powinien znać szczegółów zapisu profilu. Powinien dostać gotowe `CardDefinition`.
- Fallback musi być jawny i logowany, ale nie może spamować logów w normalnej pętli gry.
- Seed powinien być kontrolowany, żeby utrzymać deterministyczne testy.

## Definicja ukończenia

Etap jest gotowy, gdy:

- `Start Battle` używa aktywnego decku z profilu,
- scena `Battle` nadal działa odpalona bezpośrednio w Editorze,
- niepoprawny lub pusty deck ma kontrolowany fallback albo blokadę startu,
- deck AI jest tworzony w przewidywalny sposób,
- dane sesji są czyszczone po starcie bitwy,
- testy EditMode dla budowania danych startowych przechodzą,
- logika walki pozostaje bez zmian funkcjonalnych.
