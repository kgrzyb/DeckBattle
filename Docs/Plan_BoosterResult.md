# Plan: Booster Result po zwycięstwie

## Cel

Dodać prostą nagrodę po wygranym meczu: booster z 3 kartami. Nowe karty trafiają do kolekcji gracza, a duplikaty zamieniają się na shardy. Ten etap domyka podstawową pętlę MVP: start meczu, walka, zwycięstwo, nagroda, aktualizacja profilu.

## Zależności

Ten etap zakłada, że istnieją już:

- `CardId` w `CardDefinition`,
- `CardCatalog`,
- `PlayerProfile` z kolekcją i shardami,
- `PlayerProfileStore`,
- start bitwy z deckiem z profilu,
- wynik meczu w scenie `Battle`.

## Zakres

- Logika generowania boostera.
- Dodawanie nowych kart do kolekcji.
- Zamiana duplikatów na shardy.
- Zapis profilu po przyznaniu nagrody.
- Prosty ekran lub panel wyniku boostera.
- Powrót do `MainMenu` po odebraniu nagrody.
- Testy EditMode dla logiki boostera.

Ten etap nie obejmuje:

- sklepu,
- monet premium,
- animacji otwierania paczki produkcyjnej jakości,
- rzadkich efektów wizualnych,
- użycia shardów,
- ekonomii progresji poza minimalnym MVP.

## Proponowane klasy

### BoosterDefinition

`ScriptableObject` opisujący booster.

Minimalne pola:

- liczba kart w boosterze,
- pula możliwych kart,
- liczba shardów za duplikat,
- opcjonalnie wagi rzadkości.

Na MVP można zacząć od jednej wspólnej puli kart i stałej wartości shardów za duplikat.

### BoosterRewardService

Plain C# serwis przyznający nagrody.

Proponowane API:

```csharp
BoosterRewardResult OpenBooster(PlayerProfile profile, BoosterDefinition booster, CardCatalog catalog, DeterministicRandom rng);
```

Odpowiedzialność:

- losowanie kart z puli,
- sprawdzanie, czy karta jest już w kolekcji,
- dodanie nowych kart do profilu,
- naliczanie shardów za duplikaty,
- zwrócenie wyniku dla UI.

Serwis nie powinien znać sceny, UI ani `PlayerProfileStore`.

### BoosterRewardResult

Model wyniku boostera.

Minimalne pola:

- lista wylosowanych kart,
- informacja, czy karta była nowa,
- liczba shardów z każdego duplikatu,
- suma dodanych shardów.

### BoosterRewardEntry

Pojedynczy wpis wyniku boostera.

Minimalne pola:

- `CardId`,
- `CardDefinition`,
- `bool IsNew`,
- `int ShardsAwarded`.

### BoosterResultController

MonoBehaviour odpowiedzialny za panel wyniku.

Odpowiedzialność:

- pokazanie 3 kart,
- oznaczenie nowych kart i duplikatów,
- pokazanie shardów,
- obsługa przycisku powrotu do menu.

Nie powinien sam losować nagród. Powinien dostać gotowy `BoosterRewardResult`.

## Integracja z wynikiem bitwy

Po zakończeniu meczu:

- jeśli wygrał gracz, dostępny jest przycisk odebrania boostera,
- jeśli gracz przegrał, można pokazać tylko powrót do menu,
- booster jest przyznawany dokładnie raz dla danego zakończenia meczu.

Najprostszy przepływ MVP:

1. `BattleController` kończy mecz.
2. `BattleUIController` pokazuje `Victory`.
3. Gracz klika `Claim`.
4. `BoosterRewardService` przyznaje booster.
5. `PlayerProfileStore.Save(profile)` zapisuje profil.
6. `BoosterResultController` pokazuje wynik.
7. Gracz wraca do `MainMenu`.

## Dane sesji

Trzeba zdecydować, gdzie trzymany jest profil po wejściu do bitwy.

Opcje:

- ponownie załadować profil z dysku po zwycięstwie,
- przekazać referencję/profil przez sesję bitwy,
- mieć mały `GameSession`/`PlayerProfileSession`.

Rekomendacja MVP: profil ładować przez `PlayerProfileStore` przy przyznawaniu nagrody i natychmiast zapisywać po zmianie. To jest proste, odporne na reload sceny i wystarczające dla małych danych profilu.

## Losowość

Losowanie boostera powinno być kontrolowane.

Założenia:

- użyć istniejącego `DeterministicRandom` albo małego adaptera,
- testy powinny podawać seed,
- runtime może generować seed z czasu lub sesji meczu,
- nie mieszać losowości boostera z losowością walki, żeby debug bitwy pozostał czytelny.

## UI i UX MVP

Panel boostera powinien być prosty:

- tytuł `Booster Result`,
- 3 elementy kart,
- oznaczenie `New` lub `Duplicate`,
- informacja `+X shards`,
- przycisk `Continue`.

Nie dodawać ciężkich animacji ani drogich efektów. Jeśli pojawi się animacja, powinna być krótka i oparta o istniejące UI/DOTween bez generowania alokacji co klatkę.

## Testy EditMode

Dodać testy dla `BoosterRewardService`:

- booster dodaje nową kartę do kolekcji,
- duplikat daje shardy,
- wynik zawiera dokładnie liczbę kart z definicji boostera,
- nie losuje kart spoza katalogu,
- zachowuje deterministyczny wynik przy stałym seedzie,
- nie dodaje duplikatów do kolekcji,
- poprawnie sumuje shardy.

Jeśli `BoosterDefinition` będzie `ScriptableObject`, testy mogą tworzyć instancję przez `ScriptableObject.CreateInstance<BoosterDefinition>()`.

## Kolejność implementacji

1. Dodać `BoosterDefinition`.
2. Dodać `BoosterRewardEntry` i `BoosterRewardResult`.
3. Dodać `BoosterRewardService` z testami EditMode.
4. Dodać asset boostera MVP.
5. Dodać prosty `BoosterResultController`.
6. Rozszerzyć wynik zwycięstwa w `BattleUIController` o przycisk odebrania boostera.
7. Zapisać profil po przyznaniu nagrody.
8. Dodać powrót do `MainMenu`.
9. Uruchomić wąskie testy EditMode.

## Ryzyka i decyzje do sprawdzenia

- Nagroda nie może zostać przyznana wielokrotnie przez ponowne kliknięcie lub reload UI.
- Profil musi być zapisany dopiero po poprawnym naliczeniu całej nagrody.
- Losowość boostera powinna być odseparowana od walki.
- Panel wyników nie powinien wymuszać przebudowy layoutu w każdej klatce.
- Duplikaty nie mają jeszcze zastosowania poza shardami, więc UI musi jasno pokazać, co się stało.

## Definicja ukończenia

Etap jest gotowy, gdy:

- zwycięstwo pozwala odebrać booster,
- booster pokazuje 3 wyniki,
- nowe karty trafiają do kolekcji,
- duplikaty dają shardy,
- profil zapisuje się po nagrodzie,
- nagroda nie może zostać odebrana więcej niż raz za ten sam wynik,
- testy EditMode dla boostera przechodzą.
