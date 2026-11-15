using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
	[Header("State")]
	public bool canMove = true;
	public bool paused;
	public bool isSprinting, isWalking, isCrouching, isGrounded, isSliding;
	public bool onSteepSlope, isCrouchSlopeSliding, canWallJump;
	public float playerSpeed;

	[Header("Movement")]
	[SerializeField] private float walkSpeed = 5f;
	[SerializeField] private float sprintSpeed = 12f;
	[SerializeField] private float crouchSpeed = 2f;
	[SerializeField] private float airSpeed = 10f;
	[SerializeField] private float sprintAirSpeed = 14f;
	[SerializeField] private float slopeSpeed = 8f;
	[SerializeField] private float airControl = 1f;
	[SerializeField] private float inputAcceleration = 15f;
	[SerializeField] private float inputDeceleration = 15f;
	[SerializeField] private float speedAcceleration = 15f;
	public float movementFactor = 1f;
	public float jumpFactor = 1f;

	[Header("Look")]
	[Range(0.1f, 5f)] public float lookSpeed = 2f;
	[SerializeField, Range(0f, 90f)] private float lookLimit = 90f;
	[SerializeField] private float eyeOffset = 0.35f;
	[SerializeField] private float nearClip = 0.05f;

	[Header("Jumping")]
	public float jumpForce = 8f;
	[SerializeField] private float jumpSlopeForce = 8f;
	[SerializeField] private float jumpSlopeDecel = 3f;
	[SerializeField] private float gravity = 30f;
	[SerializeField] private float crouchGravity = 40f;
	[SerializeField] private float jumpGravity = 20f;
	[SerializeField] private int maxWallJumps = 1;
	[SerializeField] private float vaultMinimumAngle = 80f;
	public LayerMask landLayer = ~0;

	[Header("Capsule / Ground")]
	[SerializeField] private float capsuleRadius = 0.4f;
	[SerializeField] private float groundCheckDistance = 0.08f;
	[SerializeField] private float maxWalkableSlope = 65f;
	[SerializeField] private float stepHeight = 0.35f;
	[SerializeField] private float standHeight = 2f;
	[SerializeField] private float crouchHeight = 1f;
	[SerializeField] private float slideHeight = 0.8f;
	[SerializeField] private float crouchTransitionTime = 0.1f;

	[Header("Slide")]
	[SerializeField] private float walkSlideImpulsion = 2f;
	[SerializeField] private float walkSlideDuration = 20f;
	[SerializeField] private float sprintSlideImpulsion = 2f;
	[SerializeField] private float sprintSlideDuration = 30f;
	[SerializeField] private float slideResetTime = 1.5f;
	[SerializeField] private float forceSpeed = 30f;

	[Header("Camera")]
	public Camera playerCamera;
	public float normalFov = 80f;
	[SerializeField] private float runFOV = 90f;
	[SerializeField] private float slideFOV = 80f;
	[SerializeField] private float runSlideFOV = 94f;
	[SerializeField] private float fovEaseTime = 0.3f;
	[SerializeField] private float tiltAmount = 7f;
	[SerializeField] private float tiltSpeed = 7f;
	[SerializeField] private float slideTiltAmount = 15f;
	[SerializeField] private float swayAmount = 4f;
	[SerializeField] private float maxSway = 5f;
	[SerializeField] private float swaySmoothing = 12f;

	[Header("Landing")]
	[SerializeField] private float landRecoverSpeed = 6f;
	[SerializeField] private float landBobAmount = 0.25f;
	[SerializeField] private float landBobDuration = 0.1f;
	[SerializeField] private float fallShakeHeight = 2f;
	[SerializeField] private Vector3 fallShakeForce = new Vector3(-1f, 0.4f, -1f);
	[SerializeField] private float highFallShakeHeight = 7f;
	[SerializeField] private float highFallRollAmount = 21f;
	[SerializeField] private float highFallShakeDuration = 0.15f;
	[SerializeField] private float highFallShakeRecoverTime = 1f;
	[SerializeField] private float highFallShakeRecoverSpeed = 3f;
	[SerializeField] private float punchFrequency = 25f;
	[SerializeField] private float punchDamping = 0.35f;

	private Rigidbody rb;
	private CapsuleCollider capsule;
	private Transform orientation, cameraHolder;
	private InputAction move, jump, run, crouch;

	private Vector3 moveDirection, groundNormal = Vector3.up, dirForward, dirRight;
	private Vector2 currentInput, mouseInput;
	private float speedFactor = 1f, verticalInput, horizontalInput;
	private float yaw, rotationX, rotationZ, swayZ, currentFov, wallSlideLean;

	private bool funcSprint, crouchPress, crouchExit, slideEnd;
	private bool prejump, prejumpLand, allJumped, jumped, hitDiagonalCeiling, landBool, pendingVertical;
	private bool vaultActivate, vault, safeGrounded, downRay, slideDownRay, rightHeadCast, leftHeadCast;
	private int wallJumpsCount;
	private float wallContactTimer, aftervaultjumpTimer, slideTimer, slideResetTimer, coyoteTimer, landTimer, fallDistance;
	private Vector3 wallPointNormal;

	private Vector3 force, forceAdded;
	private float forceFactor, initforceFactor, tempforceSpeed, deceleration = 1f;

	private Vector3 pushDirection, pushFinal;
	private float pushFactor, pushDecel, pushTimer;
	private bool pushActive;

	private float groundAngle;
	private Vector3 slopeSlideMove, steepSlopeSlideMove;

	private Vector3 punchRot, punchVel;
	private float highFallRoll, highFallTarget, highFallRate;
	private float bobOffsetY, bobFrom, bobTo, bobTimer;

	private static readonly RaycastHit[] hitBuf = new RaycastHit[16];

	private bool Moving => move.ReadValue<Vector2>() != Vector2.zero;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		capsule = GetComponent<CapsuleCollider>();

		rb.useGravity = false;
		rb.freezeRotation = true;
		rb.interpolation = RigidbodyInterpolation.Interpolate;
		rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
		rb.mass = 80f;

		capsule.direction = 1;
		capsule.radius = capsuleRadius;
		capsule.height = standHeight;
		capsule.center = new Vector3(0f, standHeight / 2f, 0f);
		capsule.material = new PhysicsMaterial("PlayerNoFriction") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };

		BuildRig();
		SetupInput();

		yaw = transform.eulerAngles.y;
		orientation.localRotation = Quaternion.Euler(0f, yaw, 0f);
		transform.rotation = Quaternion.identity;
		dirForward = orientation.forward;
		dirRight = orientation.right;

		currentFov = normalFov;
		playerCamera.fieldOfView = normalFov;
		playerCamera.nearClipPlane = nearClip;
	}

	private void BuildRig()
	{
		orientation = new GameObject("Orientation").transform;
		orientation.SetParent(transform, false);

		if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
		if (playerCamera == null) playerCamera = Camera.main;
		if (playerCamera == null)
		{
			var go = new GameObject("PlayerCamera");
			playerCamera = go.AddComponent<Camera>();
			go.AddComponent<AudioListener>();
		}

		cameraHolder = new GameObject("CameraHolder").transform;
		cameraHolder.SetParent(orientation, false);
		playerCamera.transform.SetParent(cameraHolder, false);
		playerCamera.transform.localPosition = Vector3.zero;
		playerCamera.transform.localRotation = Quaternion.identity;
		UpdateCameraRigPosition();
	}

	private void SetupInput()
	{
		move = new InputAction("Move", InputActionType.Value);
		move.AddCompositeBinding("2DVector")
			.With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
			.With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
		jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
		run = new InputAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
		crouch = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
		crouch.AddBinding("<Keyboard>/c");
	}

	private void OnEnable()
	{
		if (move == null) return;
		move.Enable(); run.Enable();
		jump.Enable(); jump.performed += OnJump;
		crouch.Enable(); crouch.performed += Slide; crouch.started += SetCrouch; crouch.canceled += SetCrouch; crouch.canceled += SlideEnd;
	}

	private void OnDisable()
	{
		if (move == null) return;
		move.Disable(); run.Disable();
		jump.Disable(); jump.performed -= OnJump;
		crouch.Disable(); crouch.performed -= Slide; crouch.started -= SetCrouch; crouch.canceled -= SetCrouch; crouch.canceled -= SlideEnd;
	}

	private void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	private bool Raycast(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
	{
		int n = Physics.RaycastNonAlloc(origin, dir, hitBuf, dist, landLayer, QueryTriggerInteraction.Ignore);
		return Closest(n, out best);
	}
	private bool Raycast(Vector3 origin, Vector3 dir, float dist) => Raycast(origin, dir, dist, out _);

	private bool SphereCast(Vector3 origin, float radius, Vector3 dir, float dist, out RaycastHit best)
	{
		int n = Physics.SphereCastNonAlloc(origin, radius, dir, hitBuf, dist, landLayer, QueryTriggerInteraction.Ignore);
		return Closest(n, out best);
	}

	private bool Closest(int n, out RaycastHit best)
	{
		best = default;
		float bd = float.MaxValue;
		bool found = false;
		for (int i = 0; i < n; i++)
		{
			if (hitBuf[i].collider.transform.IsChildOf(transform)) continue;
			if (hitBuf[i].distance < bd) { bd = hitBuf[i].distance; best = hitBuf[i]; found = true; }
		}
		return found;
	}

	private void Update()
	{
		float dt = Time.deltaTime;
		HandleTimers(dt);

		dirForward = Vector3.Lerp(dirForward, orientation.forward, 13f * dt);
		dirRight = Vector3.Lerp(dirRight, orientation.right, 13f * dt);
		playerSpeed = Mathf.Round(rb.linearVelocity.magnitude * 100f) * 0.01f;

		if (!paused)
		{
			if (canMove) HandleMouseLook();
			HandleCamera();
			if (canMove)
			{
				CalculateMovementInput(dt);
				HandlePhysicsCasting();
				CheckForVault();
				HandleCrouch(dt);
				UpdateCameraRigPosition();
			}
		}
		else currentInput = Vector2.zero;

		HandlePush(dt);
		HandleMovementInput();
		HandleSlideForce(dt);
		HandleSlopeSlides(dt);
		HandleLanding(dt);
		UpdateCameraEffects(dt);

		if (canMove) UpdateStateFlags(dt);
	}

	private void UpdateStateFlags(float dt)
	{
		if (!isGrounded && moveDirection.y < 0f) fallDistance -= moveDirection.y * dt;
		if (moveDirection.y > 0f) fallDistance = 0f;
		if (isGrounded && !pendingVertical)
		{
			moveDirection.y = -1f;
			coyoteTimer = 0.15f;
			hitDiagonalCeiling = false;
		}

		safeGrounded = isGrounded || (Raycast(transform.position, Vector3.down, 0.5f) && landTimer > 0f && !allJumped && moveDirection.y < 0f);

		funcSprint = run.ReadValue<float>() > 0.1f;
		isWalking = Moving;
		isSprinting = isWalking && !isCrouching && funcSprint;

		if (isGrounded)
		{
			wallJumpsCount = 0;
			jumped = false;
			allJumped = false;
		}
	}

	private void HandleTimers(float dt)
	{
		aftervaultjumpTimer -= dt;
		slideTimer -= dt;
		slideResetTimer -= dt;
		coyoteTimer -= dt;
		landTimer -= dt;
		if (wallContactTimer > 0f)
		{
			wallContactTimer -= dt;
			if (wallContactTimer <= 0f) canWallJump = false;
		}
	}

	private void HandleMouseLook()
	{
		mouseInput = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
		rotationX = Mathf.Clamp(rotationX - mouseInput.y * 0.1f * lookSpeed, -lookLimit, lookLimit);
		yaw += mouseInput.x * 0.1f * lookSpeed;
		orientation.localRotation = Quaternion.Euler(0f, yaw, 0f);
	}

	private void OnJump(InputAction.CallbackContext ctx)
	{
		if (!canMove || paused) return;

		if (canWallJump)
		{
			wallJumpsCount++;
			Push(wallPointNormal, (Moving ? jumpSlopeForce : 7f) * 0.7f, jumpSlopeDecel);
			SetVerticalVelocity(jumpForce * 0.8f);
			Punch(-fallShakeForce * 1.5f);
			canWallJump = false;
			wallContactTimer = 0f;
		}

		if (Raycast(transform.position, Vector3.down, 1.3f) && moveDirection.y < 0f && !isGrounded)
			prejump = true;

		if (isGrounded && !prejump) DoJump();
		else if (coyoteTimer > 0f && !isGrounded && moveDirection.y < 0f) { prejump = false; DoJump(); }
		else if (aftervaultjumpTimer > 0f)
		{
			jumped = true;
			vaultActivate = true;
			prejump = false;
			aftervaultjumpTimer = 0f;
			DoJump();
		}
	}

	private void DoJump()
	{
		Sfx.Jump();
		allJumped = true;
		coyoteTimer = 0f;
		SetVerticalVelocity(jumpForce * jumpFactor);
		isGrounded = false;
		if (onSteepSlope)
		{
			Push(groundNormal, Moving ? jumpSlopeForce : 7f, jumpSlopeDecel);
			Punch(-fallShakeForce);
		}
	}

	private void SetCrouch(InputAction.CallbackContext ctx) { isCrouching = ctx.started; crouchPress = ctx.started; }

	private void Slide(InputAction.CallbackContext ctx)
	{
		if (move.ReadValue<Vector2>().y < 0f) return;
		deceleration = 1f;
		if (Moving && slideResetTimer < 0f)
		{
			if (funcSprint) slideTimer = 0.8f;
			slideResetTimer = slideResetTime;
		}
	}

	private void SlideEnd(InputAction.CallbackContext ctx) { EndSlide(); }

	private void EndSlide()
	{
		isSliding = false;
		if (forceFactor > 1f) slideEnd = true;
		isCrouching = false;
		crouchPress = false;
		slideTimer = 0f;
	}

	private void CalculateMovementInput(float dt)
	{
		Vector2 mv = move.ReadValue<Vector2>();
		float airMul = isGrounded ? 1f : airControl;

		horizontalInput = Mathf.Lerp(horizontalInput, Mathf.Sign(mv.x) * (mv.x != 0f ? 1f : 0f), (mv.x != 0f ? inputAcceleration : inputDeceleration) * airMul * dt);
		verticalInput = Mathf.Lerp(verticalInput, Mathf.Sign(mv.y) * (mv.y != 0f ? 1f : 0f), (mv.y != 0f ? inputAcceleration : inputDeceleration) * airMul * dt);

		bool moving = mv != Vector2.zero;
		float target, rate = speedAcceleration;
		if (isCrouchSlopeSliding || !moving) { target = 1f; rate = inputDeceleration; }
		else if (!isGrounded) target = funcSprint ? sprintAirSpeed : airSpeed;
		else if (isCrouching) target = crouchSpeed;
		else target = isSprinting ? sprintSpeed : walkSpeed;
		speedFactor = Mathf.Round(Mathf.Lerp(speedFactor, target, rate * dt) * 100f) * 0.01f;

		currentInput = new Vector2(Mathf.Round(verticalInput * 100f) * 0.01f, Mathf.Round(horizontalInput * 100f) * 0.01f) * speedFactor;
	}

	private float CurrentMaxSpeed()
	{
		if (!isGrounded) return isSprinting ? sprintAirSpeed : airSpeed;
		return isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
	}

	private void HandleMovementInput()
	{
		currentInput = canMove ? Vector2.ClampMagnitude(currentInput, CurrentMaxSpeed()) * movementFactor : Vector2.zero;
		float y = moveDirection.y;
		moveDirection = dirForward * currentInput.x + dirRight * currentInput.y;
		moveDirection.y = y;
	}

	private void HandleCamera()
	{
		float dt = Time.deltaTime;
		float target;
		if ((isSliding && isSprinting) || isCrouchSlopeSliding) target = runSlideFOV;
		else if (isSliding) target = slideFOV;
		else target = (isWalking && funcSprint) ? runFOV : normalFov;

		currentFov = Mathf.Lerp(currentFov, target, 1f - Mathf.Exp(-dt * 4f / Mathf.Max(0.01f, fovEaseTime)));
		playerCamera.fieldOfView = currentFov;

		float mvx = move.ReadValue<Vector2>().x;
		float tilt = ((isSliding || isCrouchSlopeSliding) ? -slideTiltAmount
			: ((isSprinting && mvx > 0f) ? -tiltAmount : ((isSprinting && mvx < 0f) ? tiltAmount : 0f))) + wallSlideLean;

		float sway = Mathf.Clamp(-mouseInput.x * 0.1f * swayAmount, -maxSway, maxSway);
		swayZ = Mathf.Lerp(swayZ, sway, dt * swaySmoothing * 2.3f);
		rotationZ = Mathf.Lerp(rotationZ, tilt, tiltSpeed * dt);
		cameraHolder.localRotation = Quaternion.Euler(rotationX, 0f, rotationZ + swayZ);
	}

	private void UpdateCameraRigPosition()
	{
		float h = capsule.height;
		float eye = h + eyeOffset + bobOffsetY;
		if (eyeOffset > 0f && SphereCast(transform.position + Vector3.up * (h - 0.2f), 0.2f, Vector3.up, eyeOffset + 0.3f, out RaycastHit roof))
			eye = Mathf.Min(eye, h + roof.distance - 0.12f);
		cameraHolder.localPosition = new Vector3(0f, eye, 0f);
	}

	public void Punch(Vector3 punch) { punchVel += punch * punchFrequency; }

	private void UpdateCameraEffects(float dt)
	{
		float w = punchFrequency;
		float c = 2f * punchDamping * w;
		punchVel += (-w * w * punchRot - c * punchVel) * dt;
		punchRot += punchVel * dt;

		highFallRoll = Mathf.MoveTowards(highFallRoll, highFallTarget, highFallRate * dt);
		if (highFallTarget == 0f) highFallRoll = Mathf.Lerp(highFallRoll, 0f, highFallShakeRecoverSpeed * dt);

		playerCamera.transform.localRotation = Quaternion.Euler(punchRot + new Vector3(0f, 0f, highFallRoll));
	}

	private IEnumerator EndHighFallShake()
	{
		yield return new WaitForSeconds(highFallShakeRecoverTime);
		highFallTarget = 0f;
	}

	private void HandleCrouch(float dt)
	{
		if (Raycast(transform.position, Vector3.up, 2f))
		{
			crouchExit = true;
			isCrouching = true;
		}
		else if (crouchExit && !crouchPress)
		{
			isCrouching = false;
			crouchExit = false;
		}

		float target = (isSliding || isCrouchSlopeSliding) ? slideHeight : (isCrouching ? crouchHeight : standHeight);
		if (!Mathf.Approximately(capsule.height, target))
		{
			float rate = (standHeight - slideHeight) / Mathf.Max(0.01f, crouchTransitionTime);
			float h = Mathf.MoveTowards(capsule.height, target, rate * dt);
			capsule.height = h;
			capsule.center = new Vector3(0f, h / 2f, 0f);
		}
		HandleSlide();
	}

	private void HandleSlide()
	{
		if (!isCrouching && !Moving) return;

		if (slideTimer > 0f && slideDownRay && moveDirection.y < 0f && isCrouching && Moving && funcSprint)
		{
			isSliding = true;
			slideTimer = 0f;
			AddHorizontalForce((isSprinting ? sprintSlideImpulsion : walkSlideImpulsion) * orientation.forward,
				isSprinting ? sprintSlideDuration : walkSlideDuration);
		}

		if (forceFactor == 0f || !slideDownRay) isSliding = false;
	}

	private void HandleSlopeSlides(float dt)
	{
		onSteepSlope = isGrounded && groundAngle > maxWalkableSlope;
		Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

		Vector3 steepTarget = onSteepSlope ? downhill * slopeSpeed : Vector3.zero;
		steepSlopeSlideMove = Vector3.MoveTowards(steepSlopeSlideMove, steepTarget, 40f * dt);

		bool crouchSlope = isGrounded && isCrouching && groundAngle > 10f && groundAngle <= maxWalkableSlope;
		isCrouchSlopeSliding = crouchSlope && slopeSlideMove.sqrMagnitude > 1f;
		Vector3 target = crouchSlope ? downhill * slopeSpeed * Mathf.Clamp01(groundAngle / 45f) : Vector3.zero;
		slopeSlideMove = Vector3.MoveTowards(slopeSlideMove, target, 20f * dt);
		if (!crouchSlope && slopeSlideMove.sqrMagnitude < 0.01f) slopeSlideMove = Vector3.zero;
	}

	private void HandlePhysicsCasting()
	{
		Vector3 p = transform.position;
		rightHeadCast = Raycast(p, orientation.right, 2f);
		leftHeadCast = Raycast(p, -orientation.right, 2f);
		if (!canWallJump) wallSlideLean = 0f;
		downRay = Raycast(p, Vector3.down, 0.3f);
		slideDownRay = Raycast(p, Vector3.down, 0.8f);
	}

	private void CheckForVault()
	{
		if (isGrounded) vaultActivate = true;
		if (safeGrounded || isCrouching) return;

		Vector3 p = transform.position;
		bool front = Raycast(p, orientation.forward, 1.4f, out RaycastHit hit) && !isGrounded;
		bool mid = Raycast(p + Vector3.up * 1.2f, orientation.forward, 1.5f);
		bool high = Raycast(p + Vector3.up * 1.8f, orientation.forward, 2f);
		bool below = Raycast(p, Vector3.down, 1f);

		if (front && !mid && !high
			&& Vector3.Angle(hit.normal, Vector3.up) > vaultMinimumAngle && Vector3.Angle(hit.normal, Vector3.up) < 130f
			&& vaultActivate && move.ReadValue<Vector2>().y > 0f && (!(moveDirection.y > 0f) || !below))
		{
			jumped = false;
			vaultActivate = false;
			aftervaultjumpTimer = 0.5f;
			Punch(new Vector3(6f, 0f, 3f));
			vault = true;
			SetVerticalVelocity(9f);
			Push(orientation.forward, 1.5f, 3f);
			StartCoroutine(DeactivateVault());
		}
	}

	private IEnumerator DeactivateVault()
	{
		yield return new WaitForSeconds(0.15f);
		if (downRay && !jumped) SetVerticalVelocity(-5f);
		else if (!jumped) SetVerticalVelocity(0f);
		yield return new WaitForSeconds(0.3f);
		vault = false;
	}

	private void HandleLanding(float dt)
	{
		if (prejump && isGrounded && moveDirection.y < 0f)
		{
			prejump = false;
			DoJump();
			prejumpLand = true;
		}

		if (slideEnd && isGrounded) { slideEnd = false; deceleration = 2.5f; }

		if (moveDirection.y < -3f && !isGrounded) landBool = true;

		if (landBool && isGrounded && !onSteepSlope)
		{
			landBool = false;
			if (!prejumpLand && !isCrouching && bobOffsetY > -0.4f)
			{
				bobFrom = bobOffsetY;
				bobTo = bobOffsetY - landBobAmount;
				bobTimer = landBobDuration;
			}
			if (fallDistance > fallShakeHeight && !vault)
				Punch(new Vector3(-fallShakeForce.x * (fallDistance > highFallShakeHeight ? 2f : 1f), 0f, 0f));
			prejumpLand = false;
			fallDistance = 0f;
		}

		if (bobTimer > 0f)
		{
			bobTimer -= dt;
			float t = 1f - Mathf.Clamp01(bobTimer / Mathf.Max(0.001f, landBobDuration));
			bobOffsetY = Mathf.SmoothStep(bobFrom, bobTo, t);
		}
		else if (bobOffsetY < -0.02f)
			bobOffsetY = Mathf.Lerp(bobOffsetY, 0f, landRecoverSpeed * dt * (isCrouching ? 7 : (isGrounded ? 1 : 5)));

		if (isGrounded) landTimer = 0.3f;

		if (onSteepSlope)
		{
			pushFinal = Vector3.zero;
			forceAdded = Vector3.zero;
		}
	}

	public void AddHorizontalForce(Vector3 direction, float factor)
	{
		deceleration = 1f;
		moveDirection = new Vector3(0f, moveDirection.y, 0f);
		forceFactor = factor;
		force = direction;
		tempforceSpeed = forceSpeed;
		initforceFactor = factor;
	}

	const float MaxAirSlideBoost = 10f;

	private void HandleSlideForce(float dt)
	{
		if (initforceFactor > 20f && tempforceSpeed < 10f) tempforceSpeed = 10f;
		else if (tempforceSpeed < 2f) tempforceSpeed = 2f;

		if (!isGrounded)
		{
			if (forceFactor < 0f) forceFactor = 0f;
			if (forceFactor > 0f) tempforceSpeed -= dt * initforceFactor / 2f;
		}
		else tempforceSpeed = forceSpeed;

		forceFactor -= dt * (tempforceSpeed * deceleration);

		if (!isGrounded && forceFactor > 0f)
		{
			float boost = force.magnitude * forceFactor;
			if (boost > MaxAirSlideBoost) forceFactor *= MaxAirSlideBoost / boost;
		}

		if (forceFactor > 0f) forceAdded = force * forceFactor;
		else
		{
			forceAdded = Vector3.zero;
			forceFactor = 0f;
			force = Vector3.zero;
		}
		if (forceFactor <= 1f) deceleration = 1f;
	}

	public void Push(Vector3 dir, float factor, float decel)
	{
		pushTimer = 0f;
		pushDirection = dir;
		pushFactor = factor;
		pushDecel = decel;
		pushActive = true;
	}

	private void HandlePush(float dt)
	{
		if (pushFactor > 0f)
		{
			pushFinal = new Vector3(pushDirection.x, 0f, pushDirection.z) * pushFactor;
			pushFactor -= dt * pushDecel;
		}
		if (pushFactor < 0f) pushFinal = Vector3.zero;
		if (pushActive && isGrounded && pushTimer > 0.5f)
		{
			pushDirection = Vector3.zero;
			pushFactor = 0f;
			pushFinal = Vector3.zero;
			pushActive = false;
		}
		pushTimer += dt;
	}

	public void SetVerticalVelocity(float y) { moveDirection.y = y; pendingVertical = true; }

	private void FixedUpdate()
	{
		float dt = Time.fixedDeltaTime;

		UpdateGround();

		if (pendingVertical) pendingVertical = false;
		else if (!isGrounded) moveDirection.y = rb.linearVelocity.y;
		else if (moveDirection.y < -1f) moveDirection.y = -1f;

		if (!isGrounded && moveDirection.y > -40f)
		{
			float g = isCrouching ? crouchGravity : (moveDirection.y > 0f ? jumpGravity : gravity);
			moveDirection.y -= g * dt;
		}

		Vector3 horiz = new Vector3(moveDirection.x + forceAdded.x + pushFinal.x, 0f, moveDirection.z + forceAdded.z + pushFinal.z);
		bool walkable = isGrounded && groundAngle <= maxWalkableSlope;
		Vector3 total;
		if (walkable) total = Vector3.ProjectOnPlane(horiz, groundNormal) + slopeSlideMove;
		else if (isGrounded) total = steepSlopeSlideMove;
		else total = horiz + new Vector3(slopeSlideMove.x, 0f, slopeSlideMove.z);
		total.y += moveDirection.y;

		rb.linearVelocity = total;

		if (walkable) StepUp(horiz);
	}

	private void UpdateGround()
	{
		float r = capsule.radius;
		Vector3 origin = transform.position + Vector3.up * (r + 0.05f);
		float dist = 0.05f + 0.1f * r + groundCheckDistance;

		if (SphereCast(origin, r * 0.9f, Vector3.down, dist, out RaycastHit groundHit) && moveDirection.y <= 0.01f)
		{
			isGrounded = true;
			groundNormal = Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f + groundCheckDistance, out RaycastHit rh) ? rh.normal : groundHit.normal;
			groundAngle = Vector3.Angle(groundNormal, Vector3.up);
		}
		else
		{
			isGrounded = false;
			groundNormal = Vector3.up;
			groundAngle = 0f;
		}
	}

	private void StepUp(Vector3 horiz)
	{
		if (horiz.sqrMagnitude < 0.25f) return;
		Vector3 dir = horiz.normalized;
		float reach = capsule.radius + 0.15f;
		if (Raycast(transform.position + Vector3.up * 0.05f, dir, reach, out RaycastHit low) && low.normal.y < 0.3f
			&& !Raycast(transform.position + Vector3.up * (stepHeight + 0.05f), dir, reach + 0.1f))
		{
			rb.position += Vector3.up * Mathf.Min(0.15f, stepHeight);
		}
	}

	private void OnCollisionEnter(Collision c) { HandleContacts(c); }
	private void OnCollisionStay(Collision c) { HandleContacts(c); }

	private void HandleContacts(Collision c)
	{
		for (int i = 0; i < c.contactCount; i++)
		{
			Vector3 n = c.GetContact(i).normal;
			float ang = Vector3.Angle(n, Vector3.up);

			if (!isGrounded && wallJumpsCount < maxWallJumps && !downRay && ang > 88f && ang < 100f)
			{
				if (rightHeadCast) wallSlideLean = 10f;
				else if (leftHeadCast) wallSlideLean = -10f;
				wallPointNormal = n;
				canWallJump = true;
				wallContactTimer = 0.08f;
			}

			if (!isGrounded && n.y < -0.2f && moveDirection.y > 0f)
			{
				if (ang >= 170f)
				{
					allJumped = false;
					SetVerticalVelocity(isSprinting ? -8f : -5f);
					Punch(new Vector3(-fallShakeForce.x, 0f, -fallShakeForce.z));
				}
				else if (!hitDiagonalCeiling)
				{
					hitDiagonalCeiling = true;
					if (ang > 130f) SetVerticalVelocity(isSprinting ? 2f : 1f);
					Push(new Vector3(n.x, 0f, n.z), 5.5f, jumpSlopeDecel * 2f);
					Punch(-fallShakeForce);
				}
			}
		}
	}
}
