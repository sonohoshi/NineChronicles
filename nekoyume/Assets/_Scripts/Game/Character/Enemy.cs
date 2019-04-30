using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using Nekoyume.Game.Controller;
using Nekoyume.Game.VFX;
using UniRx;
using UnityEngine;

namespace Nekoyume.Game.Character
{
    public class Enemy : CharacterBase
    {
        private static readonly Vector3 DamageTextForce = new Vector3(0.1f, 0.5f);
        
        public Guid id;
        
        public int DataId = 0;

        private Player _player;

        public override float Speed => -1.8f;
        
        protected override Vector3 _hudOffset => animator.GetHUDPosition();

        #region Mono

        protected override void Awake()
        {
            base.Awake();
            
            animator = new EnemyAnimator(this);
            animator.onEvent.Subscribe(OnAnimatorEvent);
            animator.SetTimeScale(AnimatorTimeScale);
            
            _targetTag = Tag.Player;
        }

        private void OnDestroy()
        {
            animator?.Dispose();
        }

        #endregion
        
        public void Init(Model.Monster spawnCharacter, Player player)
        {
            _player = player;
            InitStats(spawnCharacter);
            id = spawnCharacter.id;
            StartRun();
        }
        
        public override IEnumerator CoProcessDamage(int dmg, bool critical)
        {
            yield return StartCoroutine(base.CoProcessDamage(dmg, critical));

            var position = transform.TransformPoint(0f, 1f, 0f);
            var force = DamageTextForce;
            var txt = dmg.ToString();
            PopUpDmg(position, force, txt, critical);

//            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
//            if (renderer != null)
//            {
//                Material mat = renderer.material;
//                Sequence colorseq = DOTween.Sequence();
//                colorseq.Append(mat.DOColor(Color.red, 0.1f));
//                colorseq.Append(mat.DOColor(Color.white, 0.1f));
//            }
        }
        
        protected override bool CanRun()
        {
            return base.CanRun() && !TargetInRange(_player);
        }

        protected override void OnDead()
        {
            Event.OnEnemyDead.Invoke(this);
            base.OnDead();
        }
        
        protected override void PopUpDmg(Vector3 position, Vector3 force, string dmg, bool critical)
        {
            base.PopUpDmg(position, force, dmg, critical);

            var pos = transform.position;
            pos.x -= 0.2f;
            pos.y += 0.32f;
            
            if (critical)
            {
                VFXController.instance.Create<BattleAttackCritical01VFX>(pos).Play();
            }
            else
            {
                VFXController.instance.Create<BattleAttack01VFX>(pos).Play();    
            }
        }

        private void InitStats(Model.Monster character)
        {
            var stats = character.data.GetStats(character.level);
            HP = stats.HP;
            ATK = stats.Damage;
            DEF = stats.Defense;
            Power = 0;
            HPMax = HP;
        }
        
        private void OnAnimatorEvent(string eventName)
        {
            switch (eventName)
            {
                case "attackStart":
                    break;
                case "attackPoint":
                    Event.OnAttackEnd.Invoke(this);
                    break;
                case "footstep":
                    break;
            }
        }
    }
}
