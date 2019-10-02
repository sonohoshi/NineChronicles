using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BTAI;
using Nekoyume.Data.Table;
using Nekoyume.EnumType;
using Nekoyume.Game.CC;
using Nekoyume.Game.Controller;
using Nekoyume.Game.VFX;
using Nekoyume.Game.VFX.Skill;
using Nekoyume.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Nekoyume.Game.Character
{
    public abstract class CharacterBase : MonoBehaviour
    {
        protected const float AnimatorTimeScale = 1.2f;
        protected const float KSkillGlobalCooltime = 0.6f;

        public Root Root;
        public int HP = 0;
        public int ATK = 0;
        public int DEF = 0;

        public int Power = 100;
        public float RunSpeed = 0.0f;
        public string targetTag = "";
        public SizeType sizeType = SizeType.S;
        public Model.CharacterBase model;

        protected virtual WeightType WeightType => WeightType.Small;

        protected float dyingTime = 1.0f;

        protected HpBar _hpBar;
        private ProgressBar _castingBar;
        protected SpeechBubble _speechBubble;

        public abstract Guid Id { get; }
        public abstract float Speed { get; }
        public int HPMax { get; protected set; } = 0;
        public ICharacterAnimator animator { get; protected set; }
        public bool attackEnd { get; private set; }
        public bool hitEnd { get; private set; }
        public bool dieEnd { get; private set; }
        public bool Rooted => gameObject.GetComponent<IRoot>() != null;
        public bool Silenced => gameObject.GetComponent<ISilence>() != null;
        public bool Stunned => gameObject.GetComponent<IStun>() != null;

        protected virtual float Range { get; set; }
        protected virtual Vector3 HUDOffset => new Vector3();
        protected virtual Vector3 DamageTextForce => default;

        private bool applicationQuitting = false;

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        protected virtual void Awake()
        {
            Event.OnAttackEnd.AddListener(AttackEnd);
        }

        protected virtual void OnDisable()
        {
            RunSpeed = 0.0f;
            Root = null;
            if (!applicationQuitting)
                DisableHUD();
        }

        public bool IsDead()
        {
            return HP <= 0;
        }

        public bool IsAlive()
        {
            return !IsDead();
        }

        protected float AttackSpeedMultiplier
        {
            get
            {
                var slows = GetComponents<ISlow>();
                var multiplierBySlow = slows.Select(slow => slow.AttackSpeedMultiplier).DefaultIfEmpty(1.0f).Min();
                return multiplierBySlow;
            }
        }

        protected float RunSpeedMultiplier
        {
            get
            {
                var slows = GetComponents<ISlow>();
                var multiplierBySlow = slows.Select(slow => slow.RunSpeedMultiplier).DefaultIfEmpty(1.0f).Min();
                return multiplierBySlow;
            }
        }

        private void Run()
        {
            if (Rooted)
            {
                animator.StopRun();
                return;
            }

            animator.Run();

            Vector2 position = transform.position;
            position.x += Time.deltaTime * RunSpeed * RunSpeedMultiplier;
            transform.position = position;
        }

        protected virtual IEnumerator Dying()
        {
            StopRun();
            animator.Die();
            yield return new WaitForSeconds(.2f);
            DisableHUD();
            yield return new WaitForSeconds(.8f);
            OnDead();
        }

        protected virtual void Update()
        {
            Root?.Tick();
            if (!ReferenceEquals(_hpBar, null))
            {
                _hpBar.UpdatePosition(gameObject, HUDOffset);
            }

            if (!ReferenceEquals(_speechBubble, null))
            {
                _speechBubble.UpdatePosition(gameObject, HUDOffset);
            }
        }

        public int CalcAtk()
        {
            var r = ATK * 0.1f;
            return Mathf.FloorToInt((ATK + UnityEngine.Random.Range(-r, r)) * (Power * 0.01f));
        }

        protected void UpdateHpBar()
        {
            if (ReferenceEquals(_hpBar, null))
            {
                _hpBar = Widget.Create<HpBar>(true);
            }

            _hpBar.UpdatePosition(gameObject, HUDOffset);
            _hpBar.SetText($"{HP} / {HPMax}");
            _hpBar.SetValue((float) HP / HPMax);
        }

        protected void UpdateHpBar(Dictionary<int, Buff> buffs)
        {
            UpdateHpBar();
            _hpBar.UpdateBuff(buffs);
        }


        public bool ShowSpeech(string key, params int[] list)
        {
            if (ReferenceEquals(_speechBubble, null))
            {
                _speechBubble = Widget.Create<SpeechBubble>();
            }

            if (_speechBubble.gameObject.activeSelf)
            {
                return false;
            }

            if (list.Length > 0)
            {
                string join = string.Join("_", list.Select(x => x.ToString()).ToArray());
                key = $"{key}_{join}_";
            }
            else
            {
                key = $"{key}_";
            }

            if (!_speechBubble.SetKey(key))
            {
                return false;
            }

            if (!gameObject.activeSelf)
                return true;

            StartCoroutine(_speechBubble.CoShowText());
            return true;
        }

        public virtual IEnumerator CoProcessDamage(Model.Skill.SkillInfo info, bool isConsiderDie,
            bool isConsiderElementalType)
        {
            var dmg = info.Effect;

            if (dmg <= 0)
                yield break;

            HP -= dmg;
            HP = math.max(HP, 0);
            UpdateHpBar();
            if (isConsiderDie && IsDead())
            {
                StartCoroutine(Dying());
            }
            else
            {
                animator.Hit();
            }
        }

        protected virtual void OnDead()
        {
            animator.Idle();
            gameObject.SetActive(false);
        }

        protected void PopUpDmg(Vector3 position, Vector3 force, Model.Skill.SkillInfo info,
            bool isConsiderElementalType)
        {
            var dmg = info.Effect.ToString();
            var pos = transform.position;
            pos.x -= 0.2f;
            pos.y += 0.32f;
            if (info.Critical)
            {
                ActionCamera.instance.Shake();
                AudioController.PlayDamagedCritical();
                CriticalText.Show(position, force, dmg);
                if (info.skillCategory == SkillCategory.Normal)
                    VFXController.instance.Create<BattleAttackCritical01VFX>(pos);
            }
            else
            {
                AudioController.PlayDamaged(isConsiderElementalType
                    ? info.Elemental ?? ElementalType.Normal
                    : ElementalType.Normal);
                DamageText.Show(position, force, dmg);
                if (info.skillCategory == SkillCategory.Normal)
                    VFXController.instance.Create<BattleAttack01VFX>(pos);
            }
        }

        private void InitBT()
        {
            Root = new Root();
            Root.OpenBranch(
                BT.Selector().OpenBranch(
                    BT.If(CanRun).OpenBranch(
                        BT.Call(Run)
                    ),
                    BT.If(() => !CanRun()).OpenBranch(
                        BT.Call(StopRun)
                    )
                )
            );
        }

        public void StartRun()
        {
            RunSpeed = Speed;
            if (Root == null)
            {
                InitBT();
            }
        }

        protected virtual bool CanRun()
        {
            return !(Mathf.Approximately(RunSpeed, 0f));
        }

        private void AttackEnd(CharacterBase character)
        {
            if (ReferenceEquals(character, this))
                attackEnd = true;
        }

        // FixMe. 캐릭터와 몬스터가 겹치는 현상 있음.
        public bool TargetInRange(CharacterBase target) =>
            Range > Mathf.Abs(gameObject.transform.position.x - target.transform.position.x);

        public void StopRun()
        {
            RunSpeed = 0.0f;
            animator.StopRun();
        }

        public void DisableHUD()
        {
            if (!ReferenceEquals(_hpBar, null))
            {
                Destroy(_hpBar.gameObject);
                _hpBar = null;
            }

            if (!ReferenceEquals(_castingBar, null))
            {
                Destroy(_castingBar.gameObject);
                _castingBar = null;
            }

            if (!ReferenceEquals(_speechBubble, null))
            {
                _speechBubble.StopAllCoroutines();
                _speechBubble.gameObject.SetActive(false);
                Destroy(_speechBubble.gameObject, _speechBubble.destroyTime);
                _speechBubble = null;
            }
        }

        protected virtual void ProcessAttack(CharacterBase target, Model.Skill.SkillInfo skill, bool isLastHit,
            bool isConsiderElementalType)
        {
            if (!target) return;
            target.StopRun();
            StartCoroutine(target.CoProcessDamage(skill, isLastHit, isConsiderElementalType));
        }

        private void ProcessHeal(CharacterBase target, Model.Skill.SkillInfo info)
        {
            if (target && target.IsAlive())
            {
                target.HP = Math.Min(info.Effect + target.HP, target.HPMax);

                var position = transform.TransformPoint(0f, 1.7f, 0f);
                var force = new Vector3(-0.1f, 0.5f);
                var txt = info.Effect.ToString();
                PopUpHeal(position, force, txt, info.Critical);

                UpdateHpBar();
            }

            Event.OnUpdateStatus.Invoke();
        }

        private void PopUpHeal(Vector3 position, Vector3 force, string dmg, bool critical)
        {
            DamageText.Show(position, force, dmg);
            VFXController.instance.Create<BattleHeal01VFX>(transform, HUDOffset - new Vector3(0f, 0.4f));
        }

        private void PreAnimationForTheKindOfAttack()
        {
            attackEnd = false;
            RunSpeed = 0.0f;
        }

        private IEnumerator CoAnimationAttack(bool isCritical)
        {
            PreAnimationForTheKindOfAttack();
            if (isCritical)
            {
                animator.CriticalAttack();
            }
            else
            {
                animator.Attack();
            }

            yield return new WaitUntil(() => attackEnd);
            PostAnimationForTheKindOfAttack();
        }

        private IEnumerator CoAnimationCastAttack(bool isCritical)
        {
            PreAnimationForTheKindOfAttack();
            if (isCritical)
            {
                animator.CriticalAttack();
            }
            else
            {
                animator.CastAttack();
            }

            yield return new WaitUntil(() => attackEnd);
            PostAnimationForTheKindOfAttack();
        }

        protected virtual IEnumerator CoAnimationCast(Model.Skill.SkillInfo info)
        {
            PreAnimationForTheKindOfAttack();

            AudioController.instance.PlaySfx(AudioController.SfxCode.BattleCast);
            animator.Cast();
            var pos = transform.position;
            var effect = Game.instance.stage.skillController.Get(pos, info);
            effect.Play();
            yield return new WaitForSeconds(0.6f);

            PostAnimationForTheKindOfAttack();
        }

        private void PostAnimationForTheKindOfAttack()
        {
            var enemy = GetComponentsInChildren<CharacterBase>()
                .Where(c => c.gameObject.CompareTag(targetTag))
                .OrderBy(c => c.transform.position.x).FirstOrDefault();
            if (enemy != null && !TargetInRange(enemy))
                RunSpeed = Speed;
        }

        public IEnumerator CoAttack(IEnumerable<Model.Skill.SkillInfo> infos)
        {
            var skillInfos = infos.ToList();
            var skillInfosCount = skillInfos.Count;

            yield return StartCoroutine(CoAnimationAttack(skillInfos.Any(skillInfo => skillInfo.Critical)));

            for (var i = 0; i < skillInfosCount; i++)
            {
                var info = skillInfos[i];
                var target = Game.instance.stage.GetCharacter(info.Target);
                ProcessAttack(target, info, i == skillInfosCount - 1, false);
            }
        }

        public IEnumerator CoAreaAttack(IEnumerable<Model.Skill.SkillInfo> infos)
        {
            var skillInfos = infos.ToList();
            var skillInfosFirst = skillInfos.First();
            var skillInfosCount = skillInfos.Count;

            yield return StartCoroutine(CoAnimationCast(skillInfosFirst));

            var effectTarget = Game.instance.stage.GetCharacter(skillInfosFirst.Target);
            var effect = Game.instance.stage.skillController.Get<SkillAreaVFX>(effectTarget, skillInfosFirst);
            Model.Skill.SkillInfo trigger = null;
            if (effect.finisher)
            {
                var count = FindObjectsOfType(effectTarget.GetType()).Length;
                trigger = skillInfos.Skip(skillInfosCount - count).First();
            }

            effect.Play();
            yield return new WaitForSeconds(0.5f);

            var isTriggerOn = false;
            for (var i = 0; i < skillInfosCount; i++)
            {
                var info = skillInfos[i];
                var target = Game.instance.stage.GetCharacter(info.Target);
                yield return new WaitForSeconds(0.14f);
                if (trigger == info)
                {
                    isTriggerOn = true;

                    if (!info.Critical)
                    {
                        yield return new WaitForSeconds(0.2f);
                    }

                    if (info.Elemental == ElementalType.Fire)
                    {
                        effect.StopLoop();
                        yield return new WaitForSeconds(0.1f);
                    }

                    var coroutine = StartCoroutine(CoAnimationCastAttack(info.Critical));
                    if (info.Elemental == ElementalType.Water)
                    {
                        yield return new WaitForSeconds(0.1f);
                        effect.StopLoop();
                    }

                    yield return coroutine;
                    effect.Finisher();
                    ProcessAttack(target, info, true, true);
                    if (info.Elemental != ElementalType.Fire
                        && info.Elemental != ElementalType.Water)
                    {
                        effect.StopLoop();
                    }

                    yield return new WaitUntil(() => effect.last.isStopped);
                }
                else
                {
                    ProcessAttack(target, info, isTriggerOn, isTriggerOn);
                }
            }

            yield return new WaitForSeconds(0.5f);
        }

        public IEnumerator CoDoubleAttack(IEnumerable<Model.Skill.SkillInfo> infos)
        {
            var skillInfos = infos.ToList();
            var skillInfosFirst = skillInfos.First();
            var skillInfosCount = skillInfos.Count;
            for (var i = 0; i < skillInfosCount; i++)
            {
                var info = skillInfos[i];
                var target = Game.instance.stage.GetCharacter(info.Target);
                var first = skillInfosFirst == info;
                var effect = Game.instance.stage.skillController.Get<SkillDoubleVFX>(target, info);

                yield return StartCoroutine(CoAnimationAttack(info.Critical));
                if (first)
                {
                    effect.FirstStrike();
                }
                else
                {
                    effect.SecondStrike();
                }

                ProcessAttack(target, info, i == skillInfosCount - 1, true);
            }

            yield return new WaitForSeconds(1.2f);
        }

        public IEnumerator CoBlow(IEnumerable<Model.Skill.SkillInfo> infos)
        {
            var skillInfos = infos.ToList();
            var skillInfosCount = skillInfos.Count;

            yield return StartCoroutine(CoAnimationCast(skillInfos.First()));

            yield return StartCoroutine(CoAnimationCastAttack(skillInfos.Any(skillInfo => skillInfo.Critical)));

            for (var i = 0; i < skillInfosCount; i++)
            {
                var info = skillInfos[i];
                var target = Game.instance.stage.GetCharacter(info.Target);
                var effect = Game.instance.stage.skillController.Get<SkillBlowVFX>(target, info);
                effect.Play();
                ProcessAttack(target, info, i == skillInfosCount - 1, true);
            }
        }

        public IEnumerator CoHeal(IEnumerable<Model.Skill.SkillInfo> infos)
        {
            var skillInfos = infos.ToList();

            yield return StartCoroutine(CoAnimationCast(skillInfos.First()));

            foreach (var info in skillInfos)
            {
                var target = Game.instance.stage.GetCharacter(info.Target);
                ProcessHeal(target, info);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag(targetTag))
            {
                var character = other.gameObject.GetComponent<CharacterBase>();
                if (TargetInRange(character) && character.IsAlive())
                {
                    StopRun();
                }
            }
        }
    }
}
