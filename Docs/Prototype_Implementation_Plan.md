# Deck Battle - Prototype Implementation Plan

## 1. Cel Planu

Ten dokument opisuje pierwszy plan implementacji prototypu Deck Battle w Unity. Plan bazuje na:

- `Docs/GDD_MVP.md`
- `Docs/Unity_Systems_Spec_MVP.md`

Celem pierwszego prototypu jest jak najszybsze zbudowanie grywalnej petli walki:

```text
dobor kart -> zagranie jednostek -> ustawienie formacji -> auto-battle -> obrazenia po rundzie -> kolejna runda
```

Na tym etapie wazniejsze sa czytelne zasady, szybka iteracja i stabilny runtime niz finalna grafika.

## 2. Zakres Pierwszego Prototypu

Pierwszy prototyp obejmuje:

- jedna scena Battle,
- arena hex 5x6,
- 6 testowych jednostek,
- deck gracza ustawiony tymczasowo w Inspectorze,
- deck AI ustawiony tymczasowo w Inspectorze,
- dobieranie kart,
- zagrywanie jednostek za AP,
- zapisywanie pozycji formacji,
- przesuwanie jednostek w fazie przygotowania,
- auto-battle melee/range,
- obrazenia po rundzie z `Power`,
- koniec meczu po spadku HP do 0.

Pierwszy prototyp nie obejmuje:

- glownego menu,
- kolekcji,
- deck buildera,
- boosterow,
- zapisu gry,
- finalnych modeli,
- finalnego UI,
- PvP,
- zaklec i pulapek.

## 3. Zasada Implementacji

Najpierw powstaje logika bez ladnej prezentacji, potem minimalna prezentacja.

Priorytet:

1. Logika zasad.
2. Widoczny przeplyw rundy.
3. Podstawowy input.
4. Prosta prezentacja 3D.
5. Dopiero potem UI polish.

MonoBehaviours powinny byc cienkie. Systemy takie jak deck, board, formacja i walka powinny byc mozliwe do testowania poza scena Unity.

## 4. Milestone 0 - Przygotowanie Struktury

### Cel

Przygotowac minimalny porzadek folderow i bazowe enumy bez budowania funkcji gry.

### Zadania

- Utworzyc `Assets/DeckBattle`.
- Utworzyc minimalne foldery:
  - `Scripts`
  - `Data`
  - `Prefabs`
  - `Scenes`
  - `Tests/EditMode`
- Utworzyc podstawowe enumy:
  - `BattleSide`
  - `BattlePhase`
  - `UnitType`
  - `UnitRarity`
- Utworzyc `UnitDefinition`.
- Utworzyc `BattleConfig`.

### Kryteria Zakonczenia

- Projekt kompiluje sie bez bledow.
- Mozna utworzyc `UnitDefinition` z menu Create Asset.
- Mozna utworzyc `BattleConfig` z menu Create Asset.

### Weryfikacja

- Otworzyc Unity i sprawdzic kompilacje.
- Utworzyc testowy asset jednostki.

## 5. Milestone 1 - Hex Board Logic

### Cel

Zbudowac czysta logike planszy hex bez prezentacji.

### Zadania

- Utworzyc `HexCoord`.
- Utworzyc `HexBoard`.
- Dodac:
  - sprawdzanie, czy pole istnieje,
  - liste sasiadow,
  - dystans hexowy,
  - rozpoznanie strefy gracza i AI,
  - konwersje `HexCoord` na pozycje lokalna 3D.
- Dodac testy edit mode dla planszy.

### Kryteria Zakonczenia

- Plansza 5x6 poprawnie zwraca pola i sasiadow.
- Dystans hexowy jest deterministyczny i zgodny z oczekiwaniem.
- Strefa gracza i AI sa jednoznaczne.

### Weryfikacja

- Uruchomic edit mode tests dla `HexBoard`.

## 6. Milestone 2 - Battle Runtime State

### Cel

Zbudowac podstawowy stan meczu bez UI i bez sceny.

### Zadania

- Utworzyc:
  - `BattleState`
  - `PlayerBattleState`
  - `RuntimeUnit`
  - `CardRuntimeState`
- Dodac inicjalizacje meczu z `BattleConfig`.
- Dodac numer rundy, HP, AP i deployment slots.
- Dodac prosty kontrolowany RNG dla tasowania.

### Kryteria Zakonczenia

- Mecz moze zostac utworzony z konfiguracji.
- Obie strony maja poprawne HP.
- Runda 1 ustawia startowe AP i sloty.

### Weryfikacja

- Edit mode test inicjalizacji meczu.

## 7. Milestone 3 - Deck, Hand I Zagrywanie Jednostek

### Cel

Zaimplementowac dobieranie kart i zagrywanie jednostek do formacji.

### Zadania

- Utworzyc `DeckService`.
- Utworzyc `HandService`.
- Utworzyc `UnitPlayService`.
- Dodac walidacje:
  - za malo AP,
  - brak slotu,
  - pole poza strefa,
  - pole zajete,
  - jednostka juz zagrana.
- Po zagraniu jednostki:
  - odjac AP,
  - usunac karte z reki,
  - utworzyc `RuntimeUnit`,
  - przypisac pozycje formacji.

### Kryteria Zakonczenia

- Gracz dobiera startowa reke.
- Gracz moze zagrac jednostke na legalne pole.
- Nie mozna zagrac jednostki dwa razy.
- Nie mozna przekroczyc limitu slotow.

### Weryfikacja

- Edit mode tests dla decku, reki i walidacji zagrania.

## 8. Milestone 4 - Formation System

### Cel

Dodac przesuwanie jednostek w fazie przygotowania i powrot na zapisane pozycje.

### Zadania

- Utworzyc `FormationService`.
- Dodac przesuwanie aktywnej jednostki na inne pole.
- Dodac walidacje zajetosci pola.
- Dodac reset pozycji bojowej do pozycji formacji na start rundy.
- Dodac reset HP jednostek na start rundy.

### Kryteria Zakonczenia

- Zagrana jednostka ma zapisana pozycje formacji.
- Jednostke mozna przesunac tylko na legalne wolne pole.
- Po rundzie jednostka wraca na zapisana pozycje formacji.

### Weryfikacja

- Edit mode tests dla przesuwania i resetu formacji.

## 9. Milestone 5 - Minimalna Scena Battle

### Cel

Pokazac plansze, jednostki i podstawowy flow w Unity.

### Zadania

- Utworzyc scene `Battle`.
- Dodac kamere portretowa pod katem z gory.
- Utworzyc prefab pola hex.
- Utworzyc `BoardPresenter`.
- Wygenerowac lub ustawic plansze 5x6.
- Utworzyc prosty prefab jednostki z bryly 3D.
- Utworzyc `UnitView`.
- Utworzyc `BattleController`, ktory startuje testowy mecz.

### Kryteria Zakonczenia

- Po starcie sceny widac plansze 5x6.
- Widac poprawna perspektywe portretowa.
- Testowe jednostki moga pojawic sie na polach.
- Nie ma per-frame tworzenia i niszczenia obiektow w podstawowym flow.

### Weryfikacja

- Uruchomic scene w Unity.
- Sprawdzic czy plansza jest czytelna w aspekcie telefonu.

## 10. Milestone 6 - Input I Prosty UI Walki

### Cel

Pozwolic graczowi zagrac jednostki i potwierdzic gotowosc.

### Zadania

- Utworzyc `BattleInputController`.
- Zaimplementowac tap-select:
  - tap na karte,
  - podswietlenie legalnych pol,
  - tap na pole,
  - zagranie jednostki.
- Dodac wybor istniejacej jednostki i przesuniecie na inne pole.
- Utworzyc minimalny `BattleUIController`.
- Pokazac:
  - HP gracza,
  - HP AI,
  - AP,
  - runde,
  - sloty,
  - przycisk `Ready`.
- Utworzyc prosta reke kart UI.

### Kryteria Zakonczenia

- Gracz moze zagrac karte z reki.
- Gracz moze przesunac jednostke w fazie przygotowania.
- UI pokazuje aktualne AP i HP.
- `Ready` przechodzi do kolejnej fazy.

### Weryfikacja

- Manualny test w Play Mode.
- Sprawdzic, czy UI nie aktualizuje tekstow co klatke bez potrzeby.

## 11. Milestone 7 - AI Przygotowania

### Cel

Dodac prostego przeciwnika, ktory wystawia jednostki.

### Zadania

- Utworzyc `EnemyPreparationAI`.
- AI dobiera karty.
- AI zagrywa mozliwe jednostki za AP.
- Melee ustawia blizej frontu.
- Range ustawia dalej.
- AI wykonuje przygotowanie po kliknieciu `Ready` przez gracza.

### Kryteria Zakonczenia

- AI ma jednostki na planszy przed walka.
- AI nie lamie zasad AP, slotow ani strefy planszy.
- Przy tym samym seedzie AI zachowuje sie tak samo.

### Weryfikacja

- Edit mode test prostych decyzji AI.
- Manualny test sceny Battle.

## 12. Milestone 8 - Auto-Battle Logic

### Cel

Zaimplementowac pierwsza wersje automatycznej walki.

### Zadania

- Utworzyc:
  - `CombatSimulator`
  - `TargetingService`
  - `MovementService`
  - `DamageService`
- Dodac tick-based combat.
- Dodac wybor najblizszego celu.
- Dodac deterministyczne tie-breakery:
  - dystans,
  - najnizsze HP,
  - najnizszy `RuntimeId`.
- Dodac ruch w strone celu.
- Dodac atak, cooldown i pokonanie jednostki.
- Dodac warunek konca walki.

### Kryteria Zakonczenia

- Melee podchodzi i atakuje.
- Range atakuje z dystansu.
- Pokonana jednostka przestaje walczyc.
- Walka konczy sie, gdy jedna lub obie strony nie maja zywych jednostek.

### Weryfikacja

- Edit mode tests dla targetowania, ruchu i obrazen.
- Manualna walka w scenie Battle.

## 13. Milestone 9 - Prezentacja Walki

### Cel

Pokazac walke w czytelny sposob bez finalnych animacji.

### Zadania

- Utworzyc proste eventy walki:
  - move,
  - attack,
  - damage,
  - defeated.
- `BattlePresenter` odtwarza eventy.
- Jednostka przesuwa sie miedzy hexami.
- Atak moze byc prostym podskokiem, obrotem lub krotkim flash materialu.
- HP bar aktualizuje sie po obrazeniach.

### Kryteria Zakonczenia

- Gracz widzi, kto atakuje kogo.
- Gracz widzi pokonane jednostki.
- Po walce jednostki wracaja na pozycje formacji.

### Weryfikacja

- Manualny test czytelnosci na widoku portretowym.
- Sprawdzic brak spamowania Instantiate/Destroy podczas walki.

## 14. Milestone 10 - Round Resolution I Koniec Meczu

### Cel

Domknac petle rund i meczu.

### Zadania

- Utworzyc `RoundDamageResolver`.
- Zliczyc `Power` zywych jednostek.
- Odjac HP przeciwnikowi lub graczowi.
- Sprawdzic koniec meczu.
- Jesli mecz trwa:
  - przejsc do kolejnej rundy,
  - zwiekszyc AP,
  - zwiekszyc sloty zgodnie z configiem,
  - dobrac karty,
  - przywrocic formacje.

### Kryteria Zakonczenia

- Runda zadaje obrazenia zgodnie z ocalałymi jednostkami.
- HP poprawnie spada.
- Mecz konczy sie przy HP <= 0.
- Kolejna runda startuje z zachowanymi jednostkami na formacji.

### Weryfikacja

- Edit mode tests dla `RoundDamageResolver`.
- Manualny test kilku rund z rzedu.

## 15. Milestone 11 - Pierwszy Grywalny Prototyp

### Cel

Osiagnac pelny testowy mecz od startu do konca.

### Zadania

- Dodac 6 testowych `UnitDefinition`:
  - Guard,
  - Swordsman,
  - Brute,
  - Scout,
  - Archer,
  - Crossbowman.
- Dodac `BattleConfig` MVP.
- Ustawic deck gracza i AI w Inspectorze.
- Dodac prosty ekran wyniku w scenie Battle:
  - Victory,
  - Defeat,
  - Restart.

### Kryteria Zakonczenia

- Da sie rozegrac caly mecz.
- Da sie przegrac i wygrac.
- Podstawowa petla jest czytelna bez znajomosci kodu.
- Nie wystepuja oczywiste bledy zasad.

### Weryfikacja

- Rozegrac minimum 3 pelne mecze.
- Sprawdzic Console pod katem bledow i warningow.
- Sprawdzic Profiler orientacyjnie pod katem alokacji w trakcie walki.

## 16. Milestone 12 - Minimalne Uporzadkowanie Po Prototypie

### Cel

Usunac najwiekszy dlug techniczny przed dodawaniem kolekcji i boosterow.

### Zadania

- Usunac tymczasowe debug skroty, ktore nie sa potrzebne.
- Uporzadkowac nazwy prefabow i assetow.
- Sprawdzic, czy logika nie siedzi w widokach UI.
- Sprawdzic, czy walka nie alokuje nadmiernie w tickach.
- Dopisac brakujace testy dla miejsc, ktore czesto sie psuly podczas prototypowania.

### Kryteria Zakonczenia

- Kod jest gotowy na dodanie Collection, Deck Builder i Booster.
- Znane ograniczenia sa zapisane w dokumencie lub komentarzach przy kodzie.

## 17. Proponowany Porzadek Commitow

Jesli praca bedzie commitowana etapami, sensowny podzial:

1. Project structure and core data definitions.
2. Hex board logic and tests.
3. Battle runtime state and deck flow.
4. Formation and unit play validation.
5. Battle scene placeholder board and unit views.
6. Battle input and minimal UI.
7. Enemy preparation AI.
8. Combat simulation.
9. Combat presentation and round resolution.
10. First playable battle prototype.

## 18. Minimalne Testy Przed Uznaniem Prototypu Za Grywalny

### Logika

- Hex distance dziala poprawnie.
- Jednostka nie moze zostac zagrana na pole przeciwnika.
- Jednostka nie moze zostac zagrana bez AP.
- Jednostka nie moze zostac zagrana przy pelnych slotach.
- Jednostka nie moze zostac zagrana drugi raz.
- Przesuniecie formacji nie pozwala wejsc na zajete pole.
- Range atakuje z dystansu.
- Melee podchodzi do celu.
- Ocalale jednostki zadaja obrazenia z `Power`.

### Manualne

- Mecz startuje bez bledow w Console.
- Da sie zagrac jednostke z reki.
- Da sie przesunac jednostke przed walka.
- AI wystawia jednostki.
- Walka konczy sie sama.
- Kolejna runda przywraca formacje.
- Mecz konczy sie zwyciestwem lub porazka.

## 19. Ryzyka Podczas Implementacji

### Zbyt Wczesne UI Polish

Ryzyko: ladne karty i animacje spowolnia prace nad rdzeniem.

Decyzja: UI ma byc czytelne, ale robocze, dopoki petla walki nie dziala.

### Logika W MonoBehaviour

Ryzyko: trudne testowanie i chaotyczny przeplyw.

Decyzja: zasady gry w czystych klasach, scena tylko prezentuje i przekazuje input.

### Alokacje W Walce

Ryzyko: niestabilny frame time na mobile.

Decyzja: najpierw dopuscic prosta implementacje, ale przed rozbudowa walki sprawdzic i ograniczyc alokacje w tickach.

### Za Duza Plansza

Ryzyko: slaba czytelnosc i trudne tap targety na telefonie.

Decyzja: zaczac od 5x6.

## 20. Nastepny Plan Po Grywalnej Walce

Po ukonczeniu pierwszego prototypu Battle kolejne etapy powinny byc:

1. Collection MVP.
2. Deck Builder MVP.
3. Save local JSON.
4. Booster reward po zwyciestwie.
5. Duplikaty na shardy.
6. Pierwszy balans kosztow AP i statystyk.
7. Mobile profiling pass.

Nie dodawac zaklec, pulapek, subclass ani PvP przed potwierdzeniem, ze podstawowy Battle loop jest przyjemny.
