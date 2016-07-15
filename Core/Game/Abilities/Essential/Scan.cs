﻿using System;
using UnityEngine;
using Lockstep.Data;

namespace Lockstep
{
	[UnityEngine.DisallowMultipleComponent]
	public class Scan : ActiveAbility
	{
		private const int SearchRate = (int)(LockstepManager.FrameRate / 2);
		public const long MissModifier = FixedMath.One / 2;

		public virtual bool CanMove { get; private set; }

		protected bool CanTurn { get; private set; }

		public LSAgent Target { get; private set; }



		public bool HasTarget {
			get { return _hasTarget; }
			set {
				if (_hasTarget != value) {
					_hasTarget = value;
					if (CanMove) {
						if (_hasTarget) {
							cachedMove.CanCollisionStop = false;
						} else {
							cachedMove.CanCollisionStop = true;
						}
					}
				}
			}
		}

		public virtual string ProjCode { get { return _projectileCode; } }

		public virtual long Range { get { return _range + RangeModifier; } }

		public long BaseRange { get { return _range; } }
		//Range

		[Lockstep (true)]
		public long RangeModifier { get; set; }

		public virtual long Sight { get { return _sight; } }
		//Approximate radius that's scanned for targets

		public virtual long Damage { get { return _damage; } }
		//Damage of attack

		public long BaseDamage { get { return _damage; } }


		public virtual int AttackRate { get { return _attackRate; } }
		//Frames between each attack

		public virtual bool TrackAttackAngle { get { return _trackAttackAngle; } }
		//Whether or not to require the unit to face the target for attacking

		public long AttackAngle { get { return _attackAngle; } }
		//The angle in front of the unit that the target must be located in

		protected virtual AllegianceType TargetAllegiance { //Allegiance to the target
			get { return this._targetAllegiance; }
		}


		public virtual Vector3d ProjectileOffset { get { return _projectileOffset; } }

		private Vector3d[] cachedProjectileOffsets;

		public virtual Vector3d[] ProjectileOffsets {
			get {
				if (cachedProjectileOffsets == null) {
					cachedProjectileOffsets = new Vector3d[this._secondaryProjectileOffsets.Length + 1];
					cachedProjectileOffsets [0] = this.ProjectileOffset;
					for (int i = 0; i < this._secondaryProjectileOffsets.Length; i++) {
						cachedProjectileOffsets [i + 1] = this._secondaryProjectileOffsets [i]; 
					}
				}
				return cachedProjectileOffsets;
			}
		}
		public bool CycleProjectiles { get { return this._cycleProjectiles; } }
		//Offset of projectile

		#region Serialized Values (Further description in properties)

		[SerializeField,DataCode ("Projectiles")]
		protected string _projectileCode;
		[FixedNumber, SerializeField]
		protected long _range = FixedMath.One * 6;
		[FixedNumber, SerializeField]
		protected long _sight = FixedMath.One * 10;
		[FixedNumber, SerializeField]
		protected long _damage = FixedMath.One;
		[FrameCount, SerializeField]
		protected int _attackRate = 1 * LockstepManager.FrameRate;
		[SerializeField, EnumMask]
		protected AllegianceType _targetAllegiance = AllegianceType.Enemy;

		[SerializeField]
		protected  bool _trackAttackAngle = true;
		[FixedNumberAngle, SerializeField]
		protected  long _attackAngle = FixedMath.TenDegrees;
		[SerializeField]
		protected  Vector3d _projectileOffset;
		[SerializeField]
		protected Vector3d[] _secondaryProjectileOffsets;
		[SerializeField]
		private bool _cycleProjectiles;
		[SerializeField,FrameCount]
		protected int _windup;

		#endregion

		public int Windup { get { return _windup; } }

		[SerializeField]
		protected bool _increasePriority = true;

		public virtual bool IncreasePriority { get { return _increasePriority; } }


		//Stuff for the logic
		private bool inRange;
		private long fastRange;
		private long fastRangeToTarget;
		private Vector2d Destination;
		private int attackFrameCount;
		private Move cachedMove;
		private Turn cachedTurn;

		protected LSBody cachedBody { get { return Agent.Body; } }

		private int basePriority;
		private Health cachedTargetHealth;
		private uint targetVersion;
		private int searchCount;
		private int attackCount;
		private bool _hasTarget;
		private bool isAttackMoving;
		private bool isFocused;

		protected override void OnSetup ()
		{
			cachedTurn = Agent.GetAbility<Turn> ();
			cachedMove = Agent.GetAbility<Move> ();
			if (Sight < Range)
				_sight = Range;

			fastRange = (Range * Range);
			attackFrameCount = AttackRate;
			basePriority = cachedBody.Priority;
			CanMove = cachedMove.IsNotNull ();
			if (CanMove) {
				cachedMove.onArrive += HandleOnArrive;
				cachedMove.onGroupProcessed += _HandleMoveGroupProcessed;
			}

			CanTurn = cachedTurn.IsNotNull ();
			CachedOnHit = OnHit;
			CachedAgentValid = this.AgentValid;
		}

		private void HandleOnArrive ()
		{
			if (this.isAttackMoving) {
				if (this.HasTarget == false)
					isAttackMoving = false;
			}
		}

		protected override void OnInitialize ()
		{
			cachedBody.Priority = basePriority;
			searchCount = LSUtility.GetRandom (SearchRate) + 1;
			attackCount = 0;
			HasTarget = false;
			Target = null;
			isAttackMoving = false;
			inRange = false;
			isFocused = false;
			CycleCount = 0;

			this.Destination = Vector2d.zero;
		}

		protected override void OnSimulate ()
		{

			attackCount--;
			if (HasTarget) {
				BehaveWithTarget ();
			} else {
				BehaveWithNoTarget ();
			}
		}

		[Lockstep (true)]
		public bool IsWindingUp { get; set; }

		int windupCount;

		void StartWindup ()
		{
			windupCount = this.Windup;
			IsWindingUp = true;
			Agent.ApplyImpulse (this.FireAnimImpulse);
			OnStartWindup ();
		}

		protected virtual void OnStartWindup ()
		{

		}

		protected virtual AnimState EngagingAnimState {
			get { return AnimState.Engaging; }
		}

		protected virtual AnimImpulse FireAnimImpulse {
			get { return AnimImpulse.Fire; }
		}

		void BehaveWithTarget ()
		{
			if (Target.IsActive == false || Target.SpawnVersion != targetVersion ||
			    (this.TargetAllegiance & Agent.GetAllegiance(Target)) == 0) {
				StopEngage ();
				BehaveWithNoTarget ();
				return;
			}
			if (IsWindingUp) {
				windupCount--;
				if (windupCount < 0) {
					if (this.AgentConditional (Target)) {
						Fire ();
						this.attackCount = this.attackFrameCount - this.Windup;
						IsWindingUp = false;
					} else {
						StopEngage ();
						this.ScanAndEngage ();
					}
				}
			} else {
				Vector2d targetDirection = Target.Body._position - cachedBody._position;
				long fastMag = targetDirection.FastMagnitude ();

				if (fastMag <= fastRangeToTarget) {
					if (!inRange) {
						if (CanMove)
							cachedMove.StopMove ();

					}
					Agent.SetState (EngagingAnimState);

					long mag;
					targetDirection.Normalize (out mag);
					bool withinTurn = TrackAttackAngle == false ||
					                                 (fastMag != 0 &&
					                                 cachedBody.Forward.Dot (targetDirection.x, targetDirection.y) > 0
					                                 && cachedBody.Forward.Cross (targetDirection.x, targetDirection.y).Abs () <= AttackAngle);
					bool needTurn = mag != 0 && !withinTurn;
					if (needTurn) {
						if (CanTurn) {
							cachedTurn.StartTurnDirection (targetDirection);
						} else {

						}
					} else {
						if (attackCount <= 0) {
							StartWindup ();
						}
					}

					if (inRange == false) {
						inRange = true;
					}
				} else {
					if (CanMove) {
						if (cachedMove.IsMoving == false) {
							cachedMove.StartMove (Target.Body._position);
							cachedBody.Priority = basePriority;
						} else {
							if (Target.Body.PositionChangedBuffer || inRange) {
								cachedMove.Destination = Target.Body._position;
							}
						}
					}
                
					if (isAttackMoving || isFocused == false) {
						searchCount -= 1;
						if (searchCount <= 0) {
							searchCount = SearchRate;
							if (ScanAndEngage ()) {
							} else {
							}
						}
					}
					if (inRange == true) {
						inRange = false;
					}
                
				}
			}
		}

		void BehaveWithNoTarget ()
		{
			if (isAttackMoving || Agent.IsCasting == false) {
				if (isAttackMoving) {
					{
						searchCount -= 8;
					}
				} else {
					searchCount -= 2;
				}
				if (searchCount <= 0) {
					searchCount = SearchRate;
					if (ScanAndEngage ()) {
					}
				}
			}
		}

		public event Action<LSAgent> ExtraOnHit;

		protected virtual void OnHit (LSAgent agent)
		{
			Health healther = agent.GetAbility<Health> ();
			healther.TakeDamage (Damage);
			if (ExtraOnHit != null)
				ExtraOnHit(agent);
		}

		private Action<LSAgent> CachedOnHit;

		public void Fire ()
		{

			if (CanMove) {
				cachedMove.StopMove ();
			}
			cachedBody.Priority = IncreasePriority ? basePriority + 1 : basePriority;

			OnFire (Target);
			ProjectileManager.Fire (CurrentProjectile);

		}

		/// <summary>
		/// The projectile to be fired in OnFire.
		/// </summary>
		/// <value>The current projectile.</value>
		public LSProjectile CurrentProjectile { get; private set; }

		public int CycleCount {get; private set;}
		protected virtual void OnFire (LSAgent target)
		{
			if (this.CycleProjectiles) {
				CycleCount++;
				if (CycleCount >= ProjectileOffsets.Length) {
					CycleCount = 0;
				}
				FireProjectile (ProjectileOffsets [CycleCount], target);

			} else {
				for (int i = 0; i < ProjectileOffsets.Length; i++) {
					FireProjectile (ProjectileOffsets [i], target);
				}
			}
		}
		protected void FireProjectile (Vector3d projOffset, LSAgent target) {
			CurrentProjectile = ProjectileManager.Create (
				ProjCode,
				this.Agent,
				projOffset,
				this.TargetAllegiance,
				(other) => {
					Health healther = other.GetAbility<Health> ();
					return healther.IsNotNull () && healther.HealthAmount > 0;

				},
				CachedOnHit);

			switch (CurrentProjectile.TargetingBehavior) {
			case TargetingType.Homing:
				CurrentProjectile.InitializeHoming (target);
				break;
			case TargetingType.Timed:
				CurrentProjectile.InitializeTimed ();
				break;
			case TargetingType.Positional:
				CurrentProjectile.InitializePositional (target.Body.Position.ToVector3d (target.Body.HeightPos));
				break;
			case TargetingType.Free:
                    //TODO
				throw new System.Exception ("Not implemented yet.");
				break;
			}
		}
		public void FireProjectile(string projCode, Vector3d projOffset, Vector3d targetPos)
		{
			CurrentProjectile = ProjectileManager.Create(
				projCode,
				this.Agent,
				projOffset,
				this.TargetAllegiance,
				(other) =>
				{
					Health healther = other.GetAbility<Health>();
					return healther.IsNotNull() && healther.HealthAmount > 0;

				},
				CachedOnHit);

			switch (CurrentProjectile.TargetingBehavior)
			{
				case TargetingType.Timed:
					CurrentProjectile.InitializeTimed();
					break;
				case TargetingType.Positional:
					CurrentProjectile.InitializePositional(targetPos);
					break;
				case TargetingType.Free:
					//TODO
					throw new System.Exception("Not implemented yet.");
					break;
			}
		}

		public void Engage (LSAgent other)
		{
			if (other != Agent) {
				cachedTargetHealth = other.GetAbility<Health> ();
				if (cachedTargetHealth.IsNotNull ()) {
					OnEngage(other);

					Target = other;

					HasTarget = true;
					targetVersion = Target.SpawnVersion;
					IsCasting = true;
					fastRangeToTarget = Range + (Target.Body.IsNotNull () ? Target.Body.Radius : 0);
					fastRangeToTarget *= fastRangeToTarget;
				}
			}
		}
		protected virtual void OnEngage(LSAgent target)
		{
			
		}

		public void StopEngage (bool complete = false)
		{
			isFocused = false;
			if (complete) {
				isAttackMoving = false;
			} else {
				if (isAttackMoving) {
					if (ScanAndEngage () == false) {
						cachedMove.StartMove (this.Destination);
					} else {
					}
				} else {
					if (CanMove) {
						if (HasTarget && inRange == false) {
							cachedMove.StopMove ();
						}
					}
				}
			}

			HasTarget = false;
			Target = null;
			cachedBody.Priority = basePriority;

			IsCasting = false;
		}

		protected override void OnDeactivate ()
		{
			StopEngage (true);
		}

		protected override void OnExecute (Command com)
		{
			Agent.StopCast (this.ID);
			Vector2d pos;
			DefaultData target;
			if (com.TryGetData<Vector2d> (out pos) && CanMove) {

				if (HasTarget) {
					cachedMove.RegisterGroup (false);
				} else {
					cachedMove.RegisterGroup ();
				}

				isAttackMoving = true;
				isFocused = false;

			} else if (com.TryGetData<DefaultData> (out target) && target.Is (DataType.UShort)) {
				isFocused = true;
				isAttackMoving = false;
				LSAgent tempTarget;
				DefaultData data;
				ushort targetValue = (ushort)target.Value;
				AgentController.TryGetAgentInstance (targetValue, out tempTarget);
				Engage (tempTarget);
			}

        
		}

		protected sealed override void OnStopCast ()
		{
			StopEngage (true);
		}

		Action _handleMoveGroupProcessed;

		Action _HandleMoveGroupProcessed { get { return _handleMoveGroupProcessed ?? (_handleMoveGroupProcessed = HandleMoveGroupProcessed); } }

		void HandleMoveGroupProcessed ()
		{
			this.Destination = cachedMove.Destination;
		}

		private bool ScanAndEngage ()
		{
			LSAgent agent = DoScan ();
			if (agent == null || HasTarget && agent == Target) {
				return false;
			} else {
				Engage (agent);
				return true;
			}
		}

		protected virtual bool AgentValid (LSAgent agent)
		{
			return true;
		}

		private Func<LSAgent,bool> CachedAgentValid;

		protected Func<LSAgent,bool> AgentConditional {
			get {
				Func<LSAgent,bool> agentConditional = null;

				if (this.Damage >= 0) {
					agentConditional = (other) => {
						Health health = other.GetAbility<Health> ();
						return Agent.GlobalID != other.GlobalID && health != null && health.CanLose && CachedAgentValid (other);
					};
				} else {
					agentConditional = (other) => {
						Health health = other.GetAbility<Health> ();
						return Agent.GlobalID != other.GlobalID && health != null && health.CanGain && CachedAgentValid (other);
					};
				}
				return agentConditional;
			}
		}

		protected virtual LSAgent DoScan ()
		{
            
			Func<LSAgent,bool> agentConditional = AgentConditional;
			LSAgent agent = InfluenceManager.Scan (
				                         this.cachedBody.Position,
				                         this.Sight,
				                         agentConditional,
				                         (bite) => {
					return ((this.Agent.Controller.GetAllegiance (bite) & this.TargetAllegiance) != 0);

				}
			                         );

			return agent;
		}

		public bool ScanWithinRangeAndEngage ()
		{
			LSAgent agent = this.DoScan ();
			if (agent == null) {
				return false;
			} else {
				Engage (agent);
				return true;
			}
		}
       
		#if UNITY_EDITOR
		void OnDrawGizmos ()
		{
			Gizmos.DrawWireSphere (Application.isPlaying ? Agent.Body._visualPosition : this.transform.position, this.Range.ToFloat ()); 
		}
		#endif
	}
}