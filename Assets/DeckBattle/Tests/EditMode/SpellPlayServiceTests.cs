using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class SpellPlayServiceTests
    {
        [Test]
        public void ValidatePlay_RejectsWrongCardType()
        {
            BattleState state = CreateState();
            CardRuntimeState card = state.Player.Hand[0];
            RuntimeUnit target = AddPlayerUnit(state);

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, card, SpellTarget.ForUnit(target));

            Assert.AreEqual(PlaySpellFailReason.InvalidCardType, reason);
        }

        [Test]
        public void ValidatePlay_RejectsNonPreparationPhase()
        {
            BattleState state = CreateState();
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, TestDefinitions.CreateSpell("firebolt", 1));
            RuntimeUnit target = AddPlayerUnit(state);
            state.Phase = BattlePhase.Combat;

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, spellCard, SpellTarget.ForUnit(target));

            Assert.AreEqual(PlaySpellFailReason.NotInPreparation, reason);
        }

        [Test]
        public void ValidatePlay_RejectsReadyPlayer()
        {
            BattleState state = CreateState();
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, TestDefinitions.CreateSpell("firebolt", 1));
            RuntimeUnit target = AddPlayerUnit(state);
            state.Player.IsReady = true;

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, spellCard, SpellTarget.ForUnit(target));

            Assert.AreEqual(PlaySpellFailReason.PlayerReady, reason);
        }

        [Test]
        public void ValidatePlay_RejectsCardMissingFromHand()
        {
            BattleState state = CreateState();
            CardRuntimeState spellCard = new CardRuntimeState(500, TestDefinitions.CreateSpell("firebolt", 1));
            RuntimeUnit target = AddPlayerUnit(state);

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, spellCard, SpellTarget.ForUnit(target));

            Assert.AreEqual(PlaySpellFailReason.CardNotInHand, reason);
        }

        [Test]
        public void ValidatePlay_RejectsNotEnoughAp()
        {
            BattleState state = CreateState();
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, TestDefinitions.CreateSpell("firebolt", 2));
            RuntimeUnit target = AddPlayerUnit(state);
            state.Player.Ap = 1;

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, spellCard, SpellTarget.ForUnit(target));

            Assert.AreEqual(PlaySpellFailReason.NotEnoughAp, reason);
        }

        [Test]
        public void ValidatePlay_RejectsWrongTargetSide()
        {
            BattleState state = CreateState();
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, TestDefinitions.CreateSpell("warcry", 1));
            RuntimeUnit enemyTarget = AddEnemyUnit(state);

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, spellCard, SpellTarget.ForUnit(enemyTarget));

            Assert.AreEqual(PlaySpellFailReason.InvalidTarget, reason);
        }

        [Test]
        public void ValidatePlay_AcceptsNoneTargetWhenEffectDoesNotRequireUnit()
        {
            BattleState state = CreateState();
            SpellDefinition spell = TestDefinitions.CreateSpell(
                "focus",
                1,
                SpellEffectKind.None,
                SpellTargetingKind.None);
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, spell);

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, spellCard, SpellTarget.None());

            Assert.AreEqual(PlaySpellFailReason.None, reason);
        }

        [Test]
        public void ValidatePlay_RejectsFriendlyUnitSpellWithoutTarget()
        {
            BattleState state = CreateState();
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, TestDefinitions.CreateSpell("warcry", 1));

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, spellCard, SpellTarget.None());

            Assert.AreEqual(PlaySpellFailReason.InvalidTarget, reason);
        }

        [Test]
        public void ValidatePlay_RejectsDeadFriendlyTarget()
        {
            BattleState state = CreateState();
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, TestDefinitions.CreateSpell("warcry", 1));
            RuntimeUnit target = AddPlayerUnit(state);
            target.CurrentHp = 0;

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, spellCard, SpellTarget.ForUnit(target));

            Assert.AreEqual(PlaySpellFailReason.InvalidTarget, reason);
        }

        [Test]
        public void ValidatePlay_RejectsBuffAttackNextCombatWithNoTargeting()
        {
            BattleState state = CreateState();
            SpellDefinition spell = TestDefinitions.CreateSpell(
                "bad-warcry",
                1,
                SpellEffectKind.BuffAttackNextCombat,
                SpellTargetingKind.None);
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, spell);

            PlaySpellFailReason reason = SpellPlayService.ValidatePlay(state, state.Player, spellCard, SpellTarget.None());

            Assert.AreEqual(PlaySpellFailReason.UnsupportedEffect, reason);
        }

        [Test]
        public void PlaySpell_BuffAttackNextCombat_SpendsApMovesCardAndBuffsFriendlyTarget()
        {
            BattleState state = CreateState();
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, TestDefinitions.CreateSpell("warcry", 1, amount: 3));
            RuntimeUnit target = AddPlayerUnit(state);
            int startingAp = state.Player.Ap;

            PlaySpellResult result = SpellPlayService.PlaySpell(state, state.Player, spellCard, SpellTarget.ForUnit(target));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PlaySpellFailReason.None, result.FailReason);
            Assert.AreSame(target, result.TargetUnit);
            Assert.AreEqual(3, result.Amount);
            Assert.AreEqual(startingAp - 1, state.Player.Ap);
            Assert.AreEqual(CardLocation.Played, spellCard.Location);
            Assert.IsFalse(state.Player.Hand.Contains(spellCard));
            Assert.IsTrue(state.Player.PlayedCards.Contains(spellCard));
            Assert.AreEqual(3, target.AttackBonusNextCombat);
            Assert.AreEqual(target.Definition.MaxHp, target.CurrentHp);
            Assert.IsFalse(target.IsDefeated);
        }

        [Test]
        public void PlaySpell_NoneTarget_SpendsApAndMovesCardToPlayed()
        {
            BattleState state = CreateState();
            SpellDefinition spell = TestDefinitions.CreateSpell(
                "focus",
                1,
                SpellEffectKind.None,
                SpellTargetingKind.None,
                amount: 0);
            CardRuntimeState spellCard = AddPlayerSpellToHand(state, spell);
            int startingAp = state.Player.Ap;

            PlaySpellResult result = SpellPlayService.PlaySpell(state, state.Player, spellCard, SpellTarget.None());

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PlaySpellFailReason.None, result.FailReason);
            Assert.IsNull(result.TargetUnit);
            Assert.AreEqual(0, result.Amount);
            Assert.AreEqual(startingAp - 1, state.Player.Ap);
            Assert.AreEqual(CardLocation.Played, spellCard.Location);
            Assert.IsFalse(state.Player.Hand.Contains(spellCard));
            Assert.IsTrue(state.Player.PlayedCards.Contains(spellCard));
        }

        [Test]
        public void CombatResolver_ConsumesAttackBonusOnFirstAttack()
        {
            UnitDefinition attackerDefinition = TestDefinitions.CreateUnit("attacker", 1);
            attackerDefinition.Attack = 2;
            UnitDefinition targetDefinition = TestDefinitions.CreateUnit("target", 1);
            targetDefinition.MaxHp = 10;
            var board = new HexBoard(5, 6, 1f);
            var spawnData = new UnitSpawnData[]
            {
                new UnitSpawnData(1, attackerDefinition, BattleSide.Player, new HexCoord(0, 0), 3),
                new UnitSpawnData(2, targetDefinition, BattleSide.Enemy, new HexCoord(0, 1))
            };
            BattleSimulation simulation = BattleSimulation.Create(board, spawnData);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState target = simulation.Units[1];
            attacker.SetTarget(target);
            attacker.AttackCooldownRemaining = 0f;

            CombatResolutionResult result = CombatResolver.ResolveCombat(simulation, 0f);

            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(5, result.TotalDamage);
            Assert.AreEqual(5, target.CurrentHp);
            Assert.AreEqual(0, attacker.AttackBonusNextCombat);
        }

        private static BattleState CreateState()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            var playerDeck = new CardDefinition[]
            {
                TestDefinitions.CreateUnit("guard", 1)
            };
            var enemyDeck = new CardDefinition[]
            {
                TestDefinitions.CreateUnit("enemy-guard", 1)
            };

            return BattleState.Create(config, playerDeck, enemyDeck, 42);
        }

        private static CardRuntimeState AddPlayerSpellToHand(BattleState state, SpellDefinition spellDefinition)
        {
            var card = new CardRuntimeState(600 + state.Player.Hand.Count, spellDefinition);
            card.Location = CardLocation.Hand;
            state.Player.Hand.Add(card);
            return card;
        }

        private static RuntimeUnit AddPlayerUnit(BattleState state)
        {
            var unit = new RuntimeUnit(state.AllocateRuntimeUnitId(), TestDefinitions.CreateUnit("player-unit", 1), BattleSide.Player, new HexCoord(0, 0));
            state.Player.Units.Add(unit);
            return unit;
        }

        private static RuntimeUnit AddEnemyUnit(BattleState state)
        {
            var unit = new RuntimeUnit(state.AllocateRuntimeUnitId(), TestDefinitions.CreateUnit("enemy-unit", 1), BattleSide.Enemy, new HexCoord(0, 5));
            state.Enemy.Units.Add(unit);
            return unit;
        }
    }
}
