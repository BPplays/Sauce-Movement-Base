using System;
using System.Diagnostics;
using Sandbox;
using Sandbox.Citizen;

[Title("Sauce Character Controller")]
[Category("Physics")]
[Icon("directions_walk")]
[EditorHandle("materials/gizmo/charactercontroller.png")]

public sealed class PlayerController : Component
{

	[Property, ToggleGroup("UseCustomGravity", Label = "Use Custom Gravity")] private bool UseCustomGravity {get;set;} = true;
	[Property, ToggleGroup("UseCustomGravity"), Description("Does not change scene gravity, this is only for the player."), Title("Gravity")] public Vector3 CustomGravity {get;set;} = new Vector3(0, 0, -800f);
	public Vector3 Gravity = new Vector3(0, 0, -800f);

	[Property, ToggleGroup("UseCustomFOV", Label = "Use Custom Field Of View")] private bool UseCustomFOV {get;set;} = true;
	[Property, ToggleGroup("UseCustomFOV"), Title("Field Of View"), Range(60f, 120f)] public float CustomFOV {get;set;} = 90f;

	// Movement Properties
	[Property, Group("Movement Properties"), Description("CS2 Default: 285.98f")] public float MaxSpeed {get;set;} = 285.98f;
	[Property, Group("Movement Properties"), Description("CS2 Default: 250f")] public float MoveSpeed {get;set;} = 250f;
	[Property, Group("Movement Properties"), Description("CS2 Default: 130f")] public float ShiftSpeed {get;set;} = 130f;
	[Property, Group("Movement Properties"), Description("CS2 Default: 85f")] public float CrouchSpeed {get;set;} = 85f;
	[Property, Group("Movement Properties"), Description("CS2 Default: 80f")] public float StopSpeed {get;set;} = 80f;
	[Property, Group("Movement Properties"), Description("CS2 Default: 5.2f")] public float Friction {get;set;} = 5.2f;
	[Property, Group("Movement Properties"), Description("CS2 Default: 5.5f")] public float Acceleration {get;set;} = 5.5f;
	[Property, Group("Movement Properties"), Description("CS2 Default: 12f")] public float AirAcceleration {get;set;} = 12f;
	[Property, Group("Movement Properties"), Description("CS2 Default: 30f")] public float MaxAirWishSpeed {get;set;} = 30f;
	[Property, Group("Movement Properties"), Description("CS2 Default: 301.993378f")] public float JumpForce {get;set;} = 301.993378f;
	[Property, Group("Movement Properties"), Description("CS2 Default: false")] private bool AutoBunnyhopping {get;set;} = false;

	// Stamina Properties
	[Property, Range(0f, 100f), Group("Stamina Properties"), Description("CS2 Default: 80f")] public float MaxStamina {get;set;} = 80f;
	[Property, Range(0f, 100f), Group("Stamina Properties"), Description("CS2 Default: 60f")] public float StaminaRecoveryRate {get;set;} = 60f;
	[Property, Range(0f, 1f), Group("Stamina Properties"), Description("CS2 Default: 0.08f")] public float StaminaJumpCost {get;set;} =  0.08f;
	[Property, Range(0f, 1f), Group("Stamina Properties"), Description("CS2 Default: 0.05f")] public float StaminaLandingCost {get;set;} =  0.05f;

	// Crouch Properties
	[Property, Group("Crouch Properties")] public bool ToggleCrouch {get;set;} = false;
	[Property, Range(0f, 1f), Group("Crouch Properties")] public float MinCrouchTime {get;set;} = 0.1f;
	[Property, Range(0f, 1f), Group("Crouch Properties")] public float MaxCrouchTime {get;set;} = 0.5f;
	[Property, Range(0f, 2f), Group("Crouch Properties")] public float CrouchRecoveryRate {get;set;} = 0.33f;
	[Property, Range(0f, 1f), Group("Crouch Properties")] public float CrouchCost {get;set;} = 0.1f;

	// Other Properties
	[Property, Title("Speed Multiplier"), Description("Useful for weapons that slow you down.")] public float Weight {get;set;} =  1f;
	[Property, Description("Add 'player' tag to disable collisions with other players.")] public TagSet IgnoreLayers { get; set; } = new TagSet();
	[Property] public GameObject Body {get;set;}
	[Property] public BoxCollider CollisionBox {get;set;}

	// State Bools
	[Sync] public bool IsCrouching {get;set;} = false;
	[Sync] public bool IsNoFric {get;set;} = false;
	[Sync] public bool IsOnGround {get;set;} = false;
	[Sync] public bool IsSliding {get;set;} = false;
	[Sync] public double slide_time {get;set;} = 0.0;

	// Internal objects
	private CitizenAnimationHelper animationHelper;
	private CameraComponent Camera;
	private ModelRenderer BodyRenderer;

	[Sync] public bool trig_jmp {get;set;} = false;

	// Internal Variables
	public float Stamina = 80f;
	private float CrouchTime = 0.1f;
	private float jumpStartHeight = 0f;
	private float jumpHighestHeight = 0f;
	private bool AlreadyGrounded = true;
	private Vector2 SmoothLookAngle = Vector2.Zero; // => localLookAngle.LerpTo(LookAngle, Time.Delta / 0.1f);
	private Angles SmoothLookAngleAngles => new Angles(SmoothLookAngle.x, SmoothLookAngle.y, 0);
	private Angles LookAngleAngles => new Angles(LookAngle.x, LookAngle.y, 0);
	private float StaminaMultiplier => Stamina / MaxStamina;

	// Size
	[Property, Group("Size"), Description("CS2 Default: 16f")] private float Radius {get;set;} = 16f;
	[Property, Group("Size"), Description("CS2 Default: 72f")] private float StandingHeight {get;set;} = 72f;
	[Property, Group("Size"), Description("CS2 Default: 54f")] private float CroucingHeight {get;set;} = 54f;
	[Sync] private float Height {get;set;} = 72f;
	[Sync] private float HeightGoal {get;set;} = 72f;
	private BBox BoundingBox => new BBox(new Vector3(-Radius * GameObject.Transform.Scale.x, -Radius * GameObject.Transform.Scale.y, 0f), new Vector3(Radius * GameObject.Transform.Scale.x, Radius * GameObject.Transform.Scale.y, HeightGoal * GameObject.Transform.Scale.z));
	private int _stuckTries;

	// Synced internal vars
	[Sync] private float InternalMoveSpeed {get;set;} = 250f;
	[Sync] private Vector3 LastSize {get;set;} = Vector3.Zero;
	[Sync] public Vector3 WishDir {get;set;} = Vector3.Zero;
	[Sync] public Vector3 Velocity {get;set;} = Vector3.Zero;
	[Sync] public Vector2 LookAngle {get;set;} = Vector2.Zero;

	// Dynamic Camera Vars
	[Property, ToggleGroup("CameraRollEnabled", Label = "Camera Roll")] bool CameraRollEnabled {get;set;} = false;
	[Property, ToggleGroup("CameraRollEnabled")] float CameraRollDamping {get;set;} = 0.015f;
	[Property, ToggleGroup("CameraRollEnabled")] float CameraRollSmoothing {get;set;} = 0.2f;
	[Property, ToggleGroup("CameraRollEnabled")] float CameraRollAngleLimit {get;set;} = 30f;
	float sidetiltLerp = 0f;

	//bool IsSliding = false;
	//double slide_time = 0.0;
	bool IsRampSliding = false;

	// Fucntions to make things slightly nicer

	public void Punch(in Vector3 amount) {
		ClearGround();
		Velocity += amount;
	}


	private void OnGround() {
		if (Velocity.Dot(Vector3.Down) >= 0) {
			IsOnGround = true;
		}
	}

	private void ClearGround() {
		if (true) {
			IsOnGround = false;
		}
	}

	private static double range(double value, double min, double max) {
		if (min == max) {
			throw new ArgumentException("min and max cannot be the same value.");
		}
		return Math.Clamp((value - min) / (max - min), 0, 1);
	}

	private static double Lerp(double a, double b, double t) {
		return a + (b - a) * t;
	}

	// Character Controller Functions

	float base_hi = 0;

	private void Move(bool step) {

		if (step && IsOnGround) {
			Velocity = Velocity.WithZ(0f);
		}

		if (Velocity.Length < 0.001f) {
			Velocity = Vector3.Zero;
			return;
		}

		if (test_speed) Velocity += Vector3.Left * (float)MeterToU(100000);
		if (test_speed3) Velocity += Vector3.Left * (float)MeterToU(8500);
		if (test_speed2) Velocity = Vector3.Zero;

		Vector3 position = base.GameObject.Transform.Position;
		CharacterControllerHelper characterControllerHelper = new CharacterControllerHelper(BuildTrace(position, position), position, Velocity);
		characterControllerHelper.Bounce = 0;

		var max_stand_angle_lerp = range(UtoMeter(Velocity.Length), UtoMeter(MoveSpeed), 50);
		var max_stand_angle_min = 45.5;
		var max_stand_angle_max = 7;
		max_stand_angle = Lerp(max_stand_angle_min, max_stand_angle_max, Math.Pow(max_stand_angle_lerp, 0.1));
		characterControllerHelper.MaxStandableAngle = (float)max_stand_angle;

		// for (int i = 10; i < 100; i += 10) {
		// 	max_stand_angle_lerp = range(i, UtoMeter(MoveSpeed), 55);
		// 	max_stand_angle = Lerp(max_stand_angle_min, max_stand_angle_max, Math.Pow(max_stand_angle_lerp, 0.5));
		// 	Log.Info(i.ToString()+": "+max_stand_angle.ToString());
		// }

		if (step && IsOnGround) {
			characterControllerHelper.TryMoveWithStep(Time.Delta, 18f * GameObject.Transform.Scale.z);
		}
		else {
			//characterControllerHelper.TryMove(Time.Delta);
			// var Velocity_bu = Velocity;
			if (slide_angl || stand_angl > max_stand_angle) {
				characterControllerHelper.TryMoveWithStep(Time.Delta, 18f * GameObject.Transform.Scale.z);
				//Log.Info("stdangl max: "+ stand_angl);
			} else if (Velocity.WithZ(0).Length > MeterToU(MoveSpeed + 2)) {
				characterControllerHelper.TryMoveWithStep(Time.Delta, 4f * GameObject.Transform.Scale.z);
			} else {
				// characterControllerHelper.TryMoveWithStep(Time.Delta, 2f * GameObject.Transform.Scale.z);
				characterControllerHelper.TryMove(Time.Delta);
				// Log.Info("stdangl max: "+ stand_angl);
			}
			IsOnGround = false;
			// characterControllerHelper.Velocity = Velocity_bu;
			// Velocity = Velocity_bu;
		}

		base.Transform.Position = characterControllerHelper.Position;
		Velocity = characterControllerHelper.Velocity;
	}

	private void Move() {
		if (!TryUnstuck()) {
			if (IsOnGround) {
				Move(step: true);
			}
			else {
				if (Velocity.z > 0) {
					Move(step: false);
				} else {
					Move(step: false);
				}
			}
		}
	}

	private bool TryUnstuck() {
		if (!BuildTrace(base.Transform.Position, base.Transform.Position).Run().StartedSolid)
		{
			_stuckTries = 0;
			return false;
		}

		int num = 20;
		for (int i = 0; i < num; i++) {
			Vector3 vector = base.Transform.Position + Vector3.Random.Normal * ((float)_stuckTries / 2f);
			if (i == 0) {
				vector = base.Transform.Position + Vector3.Up * 2f;
			}

			if (!BuildTrace(vector, vector).Run().StartedSolid) {
				base.Transform.Position = vector;
				return false;
			}
		}

		_stuckTries++;
		return true;
	}

	double max_stand_angle = 20;
	float stand_angl = 0;
	bool slide_angl = false;

	private void CategorizePosition() {
		Vector3 position = base.Transform.Position;
		Vector3 to = position + Vector3.Down * 1f;
		//Vector3 to = position;
		Vector3 from = position;
		slide_angl = false;
		//bool isOnGround = IsOnGround;
		// if (!IsOnGround && (Velocity.z > 4f)) {
		// 	ClearGround();
		// 	return;
		// } else {
		// 	OnGround();
		// }

		// to.z -= (IsOnGround ? 18 : 0.1f);
		to.z -= 0.0f;

		SceneTraceResult sceneTraceResult = BuildTrace(from, to).Run();

		stand_angl = Vector3.GetAngle(in Vector3.Up, in sceneTraceResult.Normal);
		if (!sceneTraceResult.Hit || stand_angl > max_stand_angle) {
			ClearGround();
			slide_angl = true;
			if (stand_angl == 90) stand_angl = 0;
			// if (stand_angl > max_stand_angle) {
			// 	Log.Info("stdangl: "+ stand_angl+" max: "+max_stand_angle);
			// }

			//return;
		} else {
			OnGround();
			// Log.Info("stdangl: "+ stand_angl+" max: "+max_stand_angle);
		}
		// Log.Info(stand_angl);
		//if (sceneTraceResult.Normal == Vector3.Zero) stand_angl = 0;



		//if (Velocity.Normal.z < 0) OnGround(); else isOnGround = false;
		//OnGround();
		//Log.Info("velnz: "+Velocity.Normal.z.ToString());
		// GroundObject = sceneTraceResult.GameObject;
		// GroundCollider = sceneTraceResult.Shape?.Collider as Collider;

		if ((IsOnGround && !sceneTraceResult.StartedSolid && sceneTraceResult.Fraction > 0f && sceneTraceResult.Fraction < 1f) || false) { // for some reason this fixes sliding down slopes when standing still, idek
			//base.Transform.Position = sceneTraceResult.HitPosition + (Vector3.Down * (from - to)) + (Vector3.Down * 0f);
			base.Transform.Position = sceneTraceResult.EndPosition + sceneTraceResult.Normal * 0f;
			OnGround();
		}

		// if ((!IsOnGround && !sceneTraceResult.StartedSolid && sceneTraceResult.Fraction > 0f && sceneTraceResult.Fraction < 1f) || false) { // for some reason this fixes sliding down slopes when standing still, idek
		// 	//base.Transform.Position = sceneTraceResult.HitPosition + (Vector3.Down * (from - to)) + (Vector3.Down * 0f);
		// 	base.Transform.Position = sceneTraceResult.EndPosition + sceneTraceResult.Normal * 0f;
		// 	ClearGround();
		// }

		// if (base_hi != 0) StandingHeight = base_hi;
		// base_hi = StandingHeight;
		// Height = (from.z - to.z)+20f;

		// if ((!IsOnGround && !sceneTraceResult.StartedSolid && sceneTraceResult.Fraction > 0f && sceneTraceResult.Fraction < 1f) || false) { // for some reason this fixes sliding down slopes when standing still, idek
		// 	base.Transform.Position = sceneTraceResult.HitPosition + (Vector3.Down * (from - to)) + (Vector3.Down * 0.1f);
		// 	ClearGround();
		// 	var t = Velocity;
		// 	t.z = 0;
		// 	Velocity = t;
		// }
	}

	private SceneTrace BuildTrace(Vector3 from, Vector3 to) {
		return BuildTrace(base.Scene.Trace.Ray(in from, in to));
	}

	private SceneTrace BuildTrace(SceneTrace source) {
		BBox hull = BoundingBox;
		return source.Size(in hull).WithoutTags(IgnoreLayers).IgnoreGameObjectHierarchy(base.GameObject);
	}

	private double UtoMeter(double u) {
		//var utom = 32768 / 624.23;
		var utom = 39.37;
		return u / utom;
	}
	private double MeterToU(double m) {
		//var utom = 32768 / 624.23;
		var utom = 39.37;
		return m * utom;
	}

	bool can_slide = true;

	private void CanSlide() {
		can_slide = true;
		slide_time = 0.0;
	}


	string crouch_in = "Slow";
	private void crouch() {
		var st_cr = IsCrouching;
		//if (IsCrouching && IsOnGround) can_slide = false;
		if (MeterToU(Velocity.WithZ(0).Length) < 5) can_slide = false;

		if (ToggleCrouch) {
			if (Input.Pressed(crouch_in)){
				IsCrouching = !IsCrouching;
				if (can_slide) {
					IsSliding = true;
				}
			}
		} else {
			IsCrouching = Input.Down(crouch_in);
			if (can_slide && Input.Down(crouch_in)) {
				IsSliding = true;
			}
		}
	}

	bool test_speed = false;
	bool test_speed2 = false;
	bool test_speed3 = false;
	private void GatherInput() {
		WishDir = 0;

		var rot = LookAngleAngles.WithPitch(0).ToRotation();
		WishDir = (rot.Forward * Input.AnalogMove.x) + (rot.Left * Input.AnalogMove.y);
		if (!WishDir.IsNearZeroLength) WishDir = WishDir.Normal ;

		var ToggleFric = true;

		var tfric_in = "Duck";

		if (Input.Pressed("Test_speed")) {animationHelper.TriggerJump();}
		if (Input.Pressed("Test_speed2")) {test_speed2 = true; Log.Info("t");}
		if (Input.Pressed("Test_speed3")) {test_speed3 = true; Log.Info("t");}

		if (ToggleFric) {
			if (Input.Pressed(tfric_in)) IsNoFric = !IsNoFric;
		} else {
			IsNoFric = Input.Down(tfric_in);
		}

		// if (!IsCrouching && IsOnGround) {
		// 	can_slide = true;
		// } else {
		// 	can_slide = false;
		// }


		crouch();


		if (Input.Pressed(crouch_in) || Input.Released(crouch_in)) CrouchTime += CrouchCost;

		if (!IsOnGround && Input.Pressed("Jump")) {
			bufferj_time = bufferj_time_start;
			Log.Info(bufferj_time.ToString());
		}
	}

	private void UpdateCitizenAnims() {
		if (animationHelper == null) return;
		var sld_p = slide_time - 0.068;
		if (sld_p < 0) sld_p = 0;
		var dfric_mult = Math.Pow(sld_p, 3);
		var IsSliding = false;
		if (dfric_mult < 1 && slide_time > 0) {
			IsSliding = true;
		}

		if (IsSliding || IsNoFric) {
			animationHelper.WithWishVelocity(0);
			// animationHelper.WithVelocity(0);
			animationHelper.WithVelocity(WishDir * InternalMoveSpeed);
		} else {
			animationHelper.WithWishVelocity(WishDir * InternalMoveSpeed);
			animationHelper.WithVelocity(Velocity);
		}
		if (IsSliding) {
			animationHelper.Sitting = CitizenAnimationHelper.SittingStyle.Floor;
			animationHelper.SittingPose = 1;
		} else {
			animationHelper.Sitting = CitizenAnimationHelper.SittingStyle.None;
		}
		// if (trig_jmp) {
		// 	animationHelper.TriggerJump();
		// 	trig_jmp = false;
		// };

		animationHelper.AimAngle = SmoothLookAngleAngles.ToRotation();
		animationHelper.IsGrounded = IsOnGround;
		animationHelper.WithLook(SmoothLookAngleAngles.Forward, 1f, 0.75f, 0.5f);
		animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Auto;
		if (!IsSliding) animationHelper.DuckLevel = ((1 - (Height / StandingHeight)) * 3).Clamp(0, 1);

	}
	public struct si_prefix {
		public string Name { get; set; }
		public string ShortName { get; set; }
		public int Power { get; set; }

		// Constructor to initialize the struct
		public si_prefix(string name, string shortName, int power)
		{
			Name = name;
			ShortName = shortName;
			Power = power;
		}

		// Optionally, you can override ToString for better debugging
		public override string ToString()
		{
			return $"Name: {Name}, ShortName: {ShortName}, Power: {Power}";
		}
	}

	private void update_spedometer() {
		var ui_velo = Velocity.Length;

		var prefixes = new List<si_prefix> {
			new si_prefix("None", "", 0),
			new si_prefix("kilo", "k", 3),
			new si_prefix("mega", "M", 6)
		};

		var use_prefix = prefixes[0];

		for (int i = 0; true; i++) {

			if (i >= prefixes.Count) {
				break;
			}

			var pp = UtoMeter(Velocity.Length) / Math.Pow(10, prefixes[i].Power);
			if (pp < 10000) {
				// Log.Info("pp: "+pp.ToString());
				use_prefix = prefixes[i];
				break;
			}
		}

		if(IsOnGround) {
			GroundMove();
			//Camera.Components.Get<TestUI>().Speed = UtoMeter(Velocity.WithZ(0).Length).ToString("0.##");
			Camera.Components.Get<TestUI>().Speed = UtoMeter(Velocity.Length / Math.Pow(10, use_prefix.Power)).ToString("0.##");
			Camera.Components.Get<TestUI>().Mesurement = use_prefix.ShortName+"m/s";
		} else {
			AirMove();
			Camera.Components.Get<TestUI>().Speed = UtoMeter(Velocity.Length / Math.Pow(10, use_prefix.Power)).ToString("0.##");
			Camera.Components.Get<TestUI>().Mesurement = use_prefix.ShortName+"m/s";
		}
	}


	// Source engine magic functions

	private void ApplyFriction() {
		float speed, newspeed, control, drop;

		speed = Velocity.Length;

		// If too slow, return
		if (speed < 0.1f) return;

		drop = 0;

		// Apply ground friction
		if (IsOnGround)
		{
			// Bleed off some speed, but if we have less than the bleed
			// threshold, bleed the threshold amount.
			if (speed < StopSpeed) {
				control = StopSpeed;
			} else {
				control = speed;
			}
			var dfric = Friction;
			if (IsSliding) {
				dfric *= (float)slide_over;
			}
			drop += control * dfric * Time.Delta; // Add the amount to the drop amount.
		} else {
			IsSliding = false;
		}

		// Scale the velocity
		if (IsNoFric) {
			drop = 0;
		}
		newspeed = speed - drop;
		if (newspeed < 0) newspeed = 0;

		if (newspeed != speed)
		{
			newspeed /= speed; // Determine proportion of old speed we are using.
			Velocity *= newspeed; // Adjust velocity according to proportion.
		}
		if (!IsCrouching) {
			IsSliding = false;
		}
	}

	private void Accelerate(Vector3 wishDir, float wishSpeed, float accel) {
		float addspeed, accelspeed, currentspeed;

		currentspeed = Velocity.Dot(wishDir);
		addspeed = wishSpeed - currentspeed;

		if (addspeed <= 0) return;

		accelspeed = accel * wishSpeed * Time.Delta;

		if (accelspeed > addspeed) accelspeed = addspeed;

		Velocity += wishDir * accelspeed;
	}

	private void AirAccelerate(Vector3 wishDir, float wishSpeed, float accel) {
		float addspeed, accelspeed, currentspeed;

		float wishspd = wishSpeed;

		if (wishspd > MaxAirWishSpeed) wishspd = MaxAirWishSpeed;

		currentspeed = Velocity.Dot(wishDir);
		addspeed = wishspd - currentspeed;

		if (addspeed <= 0) return;

		accelspeed = accel * wishSpeed * Time.Delta;

		if (accelspeed > addspeed) accelspeed = addspeed;

		Velocity += wishDir * accelspeed;
	}


	Vector3 ui_velo;


	private void GroundMove() {
		if (AlreadyGrounded == IsOnGround) {
			Accelerate(WishDir, WishDir.Length * InternalMoveSpeed, Acceleration);
		}
		MaxSpeed = 1000000;
		AirAcceleration = (float)MeterToU(15);
		MoveSpeed = (float)MeterToU(15);
		if (Velocity.WithZ(0).Length > MaxSpeed) {
			var FixedVel = Velocity.WithZ(0).Normal * MaxSpeed;
			Velocity = Velocity.WithX(FixedVel.x).WithY(FixedVel.y);
		}
		if (Velocity.z < 0) Velocity = Velocity.WithZ(0);

		if (((AutoBunnyhopping && Input.Down("Jump")) || Input.Pressed("Jump")) || bufferj_time > 0) {

			if (bufferj_time > 0) bufferj_time = 0;

			IsSliding = false;
			var look_vel = LookAngleAngles.WithPitch(0).Forward;
			var velxy = new Vector2(Velocity.x, Velocity.y);
			var dot = Vector3.Dot(look_vel.Normal, velxy.Normal);
			Log.Info(dot);
			Log.Info("velxy "+velxy.ToString());
			Log.Info(look_vel);
			Log.Info("look a a "+LookAngleAngles.Normal.ToString());
			if (dot <= -0.2 && Velocity.Length > 0.001) {
				var speed = Velocity.Length;
				Log.Info("speed: "+speed.ToString());
				var speedm = UtoMeter(speed);
				Log.Info("speedm: "+speedm.ToString());
				var addmult = 0.5;
				var uppies_mult = 1.0;
				var uppies_favor_look = 1.0;
				if (IsCrouching) {
					addmult = 1.0;
					uppies_mult = 1.1;
					uppies_favor_look = 1.6;
				}
				var spdmam = speedm*addmult;
				Log.Info("speedm_amult: "+(spdmam).ToString());
				var add = (float)MeterToU(spdmam);
				Log.Info("add: "+add.ToString());
				Log.Info("mtou test: "+MeterToU(10).ToString());
				var newspeed = add;
				var mult_loss = 0.0f;
				//var newspeed = 1;
				var abh_up = true;
				Vector3 nvec = Vector3.Zero;
				var usenvec = false;

				var addvec = -LookAngleAngles.WithPitch(0).Forward;
				if (abh_up) {

					Log.Info("pitch: "+LookAngleAngles.pitch.ToString());
					var uppies_angle = 45;
					var loss = Math.Pow(UtoMeter(Velocity.Length) / 50, 3);
					var loss_max = 0.45;
					if (loss > loss_max) loss = loss_max;
					if (loss < 0) loss = 0;
					if (LookAngleAngles.pitch < uppies_angle) {
						addvec = -LookAngleAngles.WithPitch(0).Forward;
					} else {
						Log.Info("uppies yey!");
						usenvec = true;
						nvec = Velocity.Normal;
						addvec = -LookAngleAngles.Forward;
						addvec = addvec.Normal;
						if (uppies_favor_look != 1.0)addvec *= (float)uppies_favor_look;
						nvec += addvec;
						nvec = nvec.Normal;
						nvec *= Velocity.Length;
						Log.Info("loss: "+loss.ToString());
						nvec *= (float)(1 - loss);
						if (uppies_mult != 1.0) nvec *= (float)uppies_mult;
					}
				}

				addvec *= newspeed;
				Log.Info("velocity: "+Velocity.ToString());
				Log.Info("addvec: "+addvec.ToString());
				Log.Info("newspeed: "+newspeed.ToString());
				Log.Info("delta: "+Time.Delta.ToString());
				animationHelper.TriggerJump();
				if (mult_loss > 0) Velocity *= mult_loss;
				Velocity += addvec;
				if (usenvec) Velocity = nvec;

			}


			jumpStartHeight = GameObject.Transform.Position.z;
			jumpHighestHeight = GameObject.Transform.Position.z;

			Punch(new Vector3(0, 0, JumpForce * StaminaMultiplier));
			Stamina -= Stamina * StaminaJumpCost * 2.9625f;
			Stamina = (Stamina * 10).FloorToInt() * 0.1f;
			if (Stamina < 0) Stamina = 0;
		}
		//CategorizePosition();
	}

	private void AirMove() {
		AirAccelerate(WishDir, InternalMoveSpeed * Weight, AirAcceleration);
	}

	// Overrides

	protected override void DrawGizmos() {
		BBox box = new BBox(new Vector3(-Radius, -Radius, 0f), new Vector3(Radius, Radius, Height));
		box.Rotate(GameObject.Transform.LocalRotation.Inverse);
		Gizmo.Draw.LineBBox(in box);
	}

	protected override void OnAwake() {
		Scene.FixedUpdateFrequency = 64;

		BodyRenderer = Components.GetInChildrenOrSelf<ModelRenderer>();
		animationHelper = Components.GetInChildrenOrSelf<CitizenAnimationHelper>();

		Camera = Scene.Camera.Components.Get<CameraComponent>();

		Height = StandingHeight;
		HeightGoal = StandingHeight;

		Transform.World = Random.Shared.FromArray( Game.ActiveScene.GetAllComponents<SpawnPoint>().Select( x => x.Transform.World ).ToArray(), global::Transform.Zero );
	}

	protected override void OnFixedUpdate() {

	}

	double slide_over = 0;

	double bufferj_time = 0;

	double bufferj_time_start = 0.098;
	float GroundedTime = 0;

	protected override void OnUpdate() {
		var debug = false;
		// if (debug) {
		// 	Log = LogLevel.Warn;
		// }
		bufferj_time_start = 0.15;

		if (IsOnGround && (GroundedTime <= (float.MaxValue / 2f))) GroundedTime += Time.Delta;

		CrouchSpeed = (float)MeterToU(2.5);
		AutoBunnyhopping = false;



		UseCustomFOV = true;
		CustomFOV = 130;

		test_speed = false;
		test_speed2 = false;
		test_speed3 = false;

		UseCustomGravity = false;
		CustomGravity = Vector3.Down * (float)MeterToU(9.80665);

		Camera.ZFar = 200000;
		//Camera.ZNear = 20;


		if (CollisionBox == null) return;

		if (CollisionBox.Scale != LastSize) {
			CollisionBox.Scale = LastSize;
			CollisionBox.Center = new Vector3(0, 0, LastSize.z * 0.5f);
		}


		if (UseCustomGravity) {
			Gravity = CustomGravity;
		} else {
			Gravity = Scene.PhysicsWorld.Gravity;
		}

		if (!IsProxy) {
			GatherInput();
		}

		if (!IsSliding) {
			slide_time = 0.0;
		}

		if (bufferj_time > 0) {
			bufferj_time -= (double)Time.Delta;
		}

		if (bufferj_time < 0) {
			bufferj_time = 0;
		}

		// Crouching
		var InitHeight = HeightGoal;
		if (IsCrouching) {
			HeightGoal = CroucingHeight;
		} else {
			var startPos = GameObject.Transform.Position;
			var endPos = GameObject.Transform.Position + new Vector3(0, 0, StandingHeight * GameObject.Transform.Scale.z);
			var crouchTrace = Scene.Trace.Ray(startPos, endPos)
										.IgnoreGameObject(GameObject)
										.Size(new BBox(new Vector3(-Radius, -Radius, 0f), new Vector3(Radius * GameObject.Transform.Scale.x, Radius * GameObject.Transform.Scale.y, 0)))
										.Run();
			if (crouchTrace.Hit) {
				HeightGoal = CroucingHeight;
				IsCrouching = true;
			} else {
				HeightGoal = StandingHeight;
			}
		}
		var HeightDiff = (InitHeight - HeightGoal).Clamp(0, 10);

		InternalMoveSpeed = MoveSpeed;
		//if (IsWalking) InternalMoveSpeed = ShiftSpeed;
		if (IsCrouching) InternalMoveSpeed = CrouchSpeed;
		InternalMoveSpeed *= StaminaMultiplier * Weight;


		var ctime = 0.02f;
		MinCrouchTime = ctime;
		MaxCrouchTime = ctime;
		Height = Height.LerpTo(HeightGoal, Time.Delta / CrouchTime.Clamp(MinCrouchTime, MaxCrouchTime));

		LastSize = new Vector3(Radius * 2, Radius * 2, HeightGoal);

		Velocity += Gravity * Time.Delta * 0.5f;

		if (AlreadyGrounded != IsOnGround) {
			if (IsOnGround) {
				CanSlide();
				slide_time = 0.0;

				crouch();
				var heightMult = Math.Abs(jumpHighestHeight - GameObject.Transform.Position.z) / 46f;
				Stamina -= Stamina * StaminaLandingCost * 2.9625f * heightMult.Clamp(0, 1f);
				Stamina = (Stamina * 10).FloorToInt() * 0.1f;
				if (Stamina < 0) Stamina = 0;
			} else {
				jumpStartHeight = GameObject.Transform.Position.z;
				jumpHighestHeight = GameObject.Transform.Position.z;
			}
		} else {
			if(IsOnGround) ApplyFriction();
		}

		//ui_velo = ui_velo.LerpTo(Velocity, Time.Delta / 2);
		if (!IsProxy) {
			update_spedometer();
			AlreadyGrounded = IsOnGround;

			CrouchTime -= Time.Delta * CrouchRecoveryRate;
			CrouchTime = CrouchTime.Clamp(0f, MaxCrouchTime);

			Stamina += StaminaRecoveryRate * Time.Delta;
			if (Stamina > MaxStamina) Stamina = MaxStamina;

			if (HeightDiff > 0f) GameObject.Transform.Position += new Vector3(0, 0, HeightDiff * 0.5f);
			Velocity *= GameObject.Transform.Scale;
			Move();
			CategorizePosition();
			Velocity /= GameObject.Transform.Scale;

			Velocity += Gravity * Time.Delta * 0.5f;

			// Terminal velocity
			var term_vel = 9999999;
			if (Velocity.Length > term_vel) Velocity = Velocity.Normal * term_vel;

			if (jumpHighestHeight < GameObject.Transform.Position.z) jumpHighestHeight = GameObject.Transform.Position.z;

			can_slide = false;
			if (!IsCrouching && Velocity.Length > MeterToU(4)) {
				can_slide = true;
			}
		}

		if (IsSliding) {
			slide_over = slide_time - 0.068;
			if (slide_over < 0) slide_over = 0;
			slide_over = Math.Pow(slide_over*1.2, 4);
			if (slide_over > 1) {
				IsSliding = false;
			}
			slide_time += Time.Delta;
			ui_velo = Vector3.Zero;
		} else {
			slide_time = 0.0;
		}


		UpdateCitizenAnims();

		if (Body == null || Camera == null || BodyRenderer == null) return;

		SmoothLookAngle = SmoothLookAngle.LerpTo(LookAngle, Time.Delta / 0.035f);

		BodyRenderer.RenderType = ModelRenderer.ShadowRenderType.On;

		Body.Transform.Rotation = SmoothLookAngleAngles.WithPitch(0).ToRotation();

		if ( IsProxy )
			return;

		BodyRenderer.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
		BodyRenderer.Enabled = false;

		// var ControllerInput = Input.GetAnalog(InputAnalog.Look);
		// if (ControllerInput.Length > 1) ControllerInput = ControllerInput.Normal;
		// ControllerInput *= 25;
		// LookAngle += new Vector2((Input.MouseDelta.y - ControllerInput.y), -(Input.MouseDelta.x + ControllerInput.x)) * Preferences.Sensitivity * 0.022f;
		LookAngle += new Vector2((Input.MouseDelta.y), -(Input.MouseDelta.x)) * Preferences.Sensitivity * 0.022f;
		LookAngle = LookAngle.WithX(LookAngle.x.Clamp(-89.9999f, 89.9999f));

		var angles = LookAngleAngles;

		CameraRollEnabled = false;
		if (CameraRollEnabled) {
			sidetiltLerp = sidetiltLerp.LerpTo(Velocity.Cross(angles.Forward).z * CameraRollDamping * (Velocity.WithZ(0).Length / MoveSpeed), Time.Delta / CameraRollSmoothing).Clamp(-CameraRollAngleLimit, CameraRollAngleLimit);
			angles = angles + new Angles(0, 0, sidetiltLerp);
		}

		Camera.Transform.Position = GameObject.Transform.Position + new Vector3(0, 0, Height * 0.89f * GameObject.Transform.Scale.z);
		Camera.Transform.Rotation = angles.ToRotation();

		if (UseCustomFOV) {
			Camera.FieldOfView = CustomFOV;
		} else {
			Camera.FieldOfView = Preferences.FieldOfView;
		}

		if (!IsOnGround) GroundedTime = 0f;

	}

}
