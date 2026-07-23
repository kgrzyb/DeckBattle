# Plan dodania szczegółowego widoku karty

## Cel

Dodać szczegółowy popup karty widoczny na górze ekranu podczas interakcji z kartą w ręce gracza.

Widok powinien pojawiać się:

- po szybkim tapnięciu karty;
- gdy gracz przytrzyma kartę i rozpocznie jej przeciąganie w celu wystawienia.

Rozwiązanie powinno korzystać z istniejących danych kart i jednostek, pozostać lekkie dla urządzeń mobilnych oraz nie mieszać logiki gameplayu z prezentacją UI.

## Obecny stan

- `CardView` obsługuje `IPointerDownHandler`, `IPointerUpHandler` i `IDragHandler`.
- Przeciąganie karty zaczyna się po `holdToDragSeconds`.
- `BattleInputController.BeginCardDrag(...)` rozpoczyna tryb przeciągania i pokazuje obecny ghost karty.
- `BattleUIController` zarządza HUD-em, ręką gracza i ghostem przeciąganej karty.
- Dane statystyk są już dostępne przez `CardRuntimeState.Definition`.
- `UnitDefinition` zawiera statystyki potrzebne do popupu: koszt AP, HP, atak, power, zasięg, crit, cooldown, mana, armor, armor penetration i `CardArt`.

## Decyzje produktowe

- Popup jest pojedynczą instancją UI pod głównym Canvasem bitwy.
- Popup jest zakotwiczony przy górnej części ekranu i respektuje safe area lub odpowiedni górny margines.
- Tapnięcie karty pokazuje popup bez rozpoczynania wystawiania.
- Przytrzymanie karty pokazuje ten sam popup i rozpoczyna istniejący flow wystawiania.
- Popup znika po zakończeniu przeciągania, po wystawieniu karty, po tapnięciu poza kartą/popupem albo po rozpoczęciu innej akcji na planszy.
- Popup nie powinien blokować legalnego wystawienia karty.

## Proponowana architektura

### CardDetailsPopupView

Dodać nowy komponent UI:

```text
Assets/DeckBattle/Scripts/UI/CardDetailsPopupView.cs
```

Odpowiedzialność komponentu:

- renderowanie szczegółów jednej karty;
- brak logiki gameplayu;
- aktualizacja tekstów i obrazów tylko wtedy, gdy zmieni się pokazywana karta;
- ukrywanie i czyszczenie widoku.

Sugerowane pola:

- `CanvasGroup` lub root `GameObject`;
- `TextMeshProUGUI` dla nazwy;
- `TextMeshProUGUI` dla kosztu AP;
- `TextMeshProUGUI` dla HP;
- `TextMeshProUGUI` dla Attack;
- `TextMeshProUGUI` dla Power;
- `TextMeshProUGUI` dla Attack Range;
- `TextMeshProUGUI` dla Crit Chance i Crit Multiplier;
- `TextMeshProUGUI` dla Attack Cooldown;
- `TextMeshProUGUI` dla Mana Threshold;
- `TextMeshProUGUI` dla Armor i Armor Penetration;
- `Image` dla `CardArt`.

Sugerowane API:

```csharp
public void Show(CardRuntimeState card);
public void Hide();
public bool IsShownFor(CardRuntimeState card);
```

## Integracja z BattleUIController

Rozszerzyć `BattleUIController`:

- dodać serialized field `CardDetailsPopupView cardDetailsPopup`;
- dodać publiczne metody `ShowCardDetails(CardRuntimeState card)` i `HideCardDetails()`;
- wywoływać `HideCardDetails()` w `Awake()`;
- podczas `RefreshHand(...)` ukryć popup, jeśli aktualnie pokazywana karta nie znajduje się już w ręce;
- nie odświeżać popupu w każdej klatce ani przy każdym `Refresh()`, jeśli karta się nie zmieniła.

Przykładowa odpowiedzialność:

```text
BattleUIController:
- zna referencję do popupu;
- decyduje kiedy pokazać/ukryć widok;
- nie interpretuje statystyk gameplayu poza przekazaniem karty do widoku.
```

## Integracja z CardView

Rozszerzyć `CardView`, żeby odróżniał szybkie tapnięcie od przytrzymania i dragowania.

Zmiany:

- zapisać pozycję pointera z `OnPointerDown`;
- dodać próg ruchu, np. `tapMoveThresholdPixels`;
- w `OnPointerUp` wykrywać tapnięcie tylko wtedy, gdy:
  - pointer nadal dotyczy tej samej interakcji;
  - karta nie weszła w tryb dragowania;
  - ruch palca/myszy nie przekroczył progu;
- po wykryciu tapnięcia wywołać metodę pokazującą szczegóły karty.

Rekomendacja:

- `CardView` nie powinien mieć bezpośredniej referencji do popupu;
- najlepiej przejść przez `BattleInputController` albo istniejącą referencję do `BattleUIController`, aby zachować jeden punkt kontroli wejścia.

## Integracja z BattleInputController

Rozszerzyć `BattleInputController` o metody pośredniczące:

```csharp
public void ShowCardDetails(CardRuntimeState card);
public void HideCardDetails();
```

W `BeginCardDrag(...)`:

- po pozytywnej walidacji karty i fazy gry wywołać `uiController.ShowCardDetails(card)`;
- zachować obecne pokazywanie ghosta karty.

W `EndCardDrag(...)`:

- po zakończeniu dragowania ukryć ghost;
- ukryć popup szczegółów, jeśli wybrany UX zakłada powrót do czystego widoku planszy po puszczeniu karty.

W `HandleBoardTap(...)` i `ClearSelection()`:

- ukryć popup przy interakcji z planszą, wybieraniu jednostki albo anulowaniu wyboru.

## Prefab i scena

Zmiany w UI:

- utworzyć prefab lub obiekt sceny dla popupu, np. `PF_CardDetailsPopup`;
- umieścić go pod głównym Canvasem bitwy;
- zakotwiczyć do góry ekranu;
- zadbać o czytelny layout na telefonach;
- użyć prostych komponentów UI: `Image`, `TextMeshProUGUI`, ewentualnie `HorizontalLayoutGroup`/`VerticalLayoutGroup`, ale bez przebudowywania layoutu co klatkę;
- jeśli layout używa grup automatycznych, aktywować je tylko przy zmianie karty, a nie w pętli update.

## Dane do pokazania

Minimalny zestaw:

- nazwa;
- koszt AP;
- HP;
- Attack;
- Power;
- Attack Range;
- Armor;
- Armor Penetration.

Rozszerzony zestaw:

- Crit Chance;
- Crit Multiplier;
- Attack Cooldown;
- Mana Threshold;
- Mana per Attack;
- Mana per Damage Taken;
- typ jednostki;
- rzadkość;
- grafika karty.

## Kolejność implementacji

1. Dodać `CardDetailsPopupView`.
2. Rozszerzyć `BattleUIController` o API pokazywania i ukrywania szczegółów karty.
3. Dodać detekcję tapnięcia w `CardView`.
4. Podłączyć pokazywanie popupu przy tapnięciu.
5. Podłączyć pokazywanie popupu w `BattleInputController.BeginCardDrag(...)`.
6. Podłączyć ukrywanie popupu przy zakończeniu dragowania i interakcjach z planszą.
7. Dodać prefab/obiekt UI popupu w scenie bitwy.
8. Zweryfikować ręcznie tap, hold, drag i wystawienie karty.
9. Uruchomić najwęższe relevantne testy EditMode.

## Testy i weryfikacja

Do sprawdzenia ręcznie w Unity:

- szybkie tapnięcie karty pokazuje popup;
- popup pokazuje właściwe dane aktualnie tapniętej karty;
- tapnięcie innej karty aktualizuje popup bez tworzenia kolejnej instancji;
- przytrzymanie karty pokazuje popup i nadal rozpoczyna drag;
- przeciągnięcie na legalne pole nadal wystawia kartę;
- przeciągnięcie na nielegalne pole nadal anuluje wystawienie;
- popup znika po zakończeniu dragowania lub interakcji z planszą;
- popup nie zasłania krytycznych elementów HUD-u na popularnych proporcjach telefonu;
- popup respektuje safe area.

Możliwe testy EditMode:

- jeśli detekcja tap vs drag zostanie wydzielona do małej klasy pomocniczej, przetestować:
  - krótki pointer down/up bez ruchu zwraca tap;
  - ruch powyżej progu nie zwraca tap;
  - wejście w drag blokuje tap;
  - długi hold uruchamia ścieżkę dragowania.

## Uwagi wydajnościowe

- Utrzymać jedną instancję popupu zamiast tworzyć/destroyować UI przy każdym tapnięciu.
- Nie używać LINQ ani alokujących formatowań w hot path.
- Aktualizować teksty tylko przy zmianie karty.
- Nie odpalać animacji ani layout rebuildów co klatkę.
- Popup powinien być pasywnym overlayem, nie nowym systemem gameplayowym.

## Ryzyka

- Największe ryzyko to konflikt między szybkim tapnięciem a istniejącym hold-to-drag.
- Zbyt agresywne ukrywanie popupu może powodować migotanie podczas przejścia z hold do drag.
- Popup na górze ekranu może kolidować z HUD-em, więc finalny layout sceny wymaga ręcznej weryfikacji na proporcjach mobilnych.
- Jeśli popup dostanie aktywny raycast target, może przypadkowo blokować drag lub tapnięcia na planszy.
