using System;
using UnityEngine;

public class BeamCannon : MonoBehaviour
{
   public bool firing = false;

   [Header("General")]
   public float chargeTime = 3.5f;
   public float duration = 3.5f;
   public float reloadTime = 10.0f;   

   [Header("Weapon")]
   public float maxRange = 5000.0f;
   public LayerMask hitMask = -1;
   public Transform target;

   [Header("Effects")]
   public ParticleSystem chargeFxPrefab;
   public ParticleSystem fireFxPrefab;
   public ParticleSystem impactFxPrefab;
   public Gradient beamColor = new Gradient();
   public Material beamMaterial;
   public float beamWidth = 1.0f;
   public float impactOffset = 0.1f;

   [Header("Audio")]
   public AudioClip beamUpClip;
   public AudioClip beamDownClip;
   public AudioClip beamFireLoopClip;
   public AudioClip beamShotClip;

   public event Action OnBeamStartCharging;
   public event Action OnBeamStartFiring;
   public event Action OnBeamCoolingDown;
   public event Action OnBeamUpdate;

   private BeamCannonBeam beam;
   private BeamCannonSound soundEffects;
   private BeamCannonParticles particleEffects;
   private BeamState state = BeamState.Ready;

   private Quaternion startLocalRot = Quaternion.identity;

   private bool firingLastFrame = false;

   private float fireFinishTime = -float.MaxValue;
   private float chargeCooldown = 1.0f;
   private float fireCooldown = 3.0f;

   private enum BeamState
   {
      Ready,
      Charging,
      Firing,
      CoolingDown
   }

   // Use this for initialization
   private void Start()
   {
      // Create a child GameObject to hold the audio sources for organization.
      Transform soundTransform = new GameObject("Sounds").transform;
      soundTransform.SetParent(transform);
      soundTransform.localPosition = Vector3.zero;
      soundTransform.localRotation = Quaternion.identity;

      // Create all the subclasses.
      soundEffects = new BeamCannonSound(soundTransform.gameObject, beamUpClip, beamDownClip, beamFireLoopClip, beamShotClip);
      particleEffects = new BeamCannonParticles(transform, chargeFxPrefab, fireFxPrefab, impactFxPrefab, impactOffset);
      beam = new BeamCannonBeam(transform, beamColor, beamMaterial, beamWidth, maxRange, hitMask);

      // ======= Hook up events for the sub classes. ========
      OnBeamStartCharging += particleEffects.Charge;
      OnBeamStartCharging += soundEffects.Charge;

      OnBeamStartFiring += particleEffects.Fire;
      OnBeamStartFiring += beam.FireAtTarget;
      OnBeamStartFiring += soundEffects.Fire;

      OnBeamCoolingDown += particleEffects.Cooldown;
      OnBeamCoolingDown += particleEffects.StopImpacting;
      OnBeamCoolingDown += beam.StopFiring;
      OnBeamCoolingDown += soundEffects.Cooldown;

      OnBeamUpdate += beam.Update;

      beam.OnBeamImpacting += particleEffects.StartImpacting;
      beam.OnBeamMissing += particleEffects.StopImpacting;
      // ====================================================

      startLocalRot = transform.localRotation;
      firingLastFrame = false;
   }

   // Update is called once per frame
   private void Update()
   {
      beam.target = target;
      MoveTowardsZero(ref chargeCooldown);
      MoveTowardsZero(ref fireCooldown);

      // Always turn to face the target. In a real game, this would need more complex
      // logic and limitations so as to not aim backwards or through the ship.
      TrackTarget();

      OnBeamUpdate?.Invoke();

      if (firing && !firingLastFrame && state == BeamState.Ready)
      {
         // Start charging beam.
         chargeCooldown = chargeTime;

         state = BeamState.Charging;
         OnBeamStartCharging?.Invoke();

         //Debug.Log($"{name}: Charging beam!");
      }

      if (chargeCooldown <= 0.0f && state == BeamState.Charging)
      {
         // Charging finished, start firing the beam.
         fireCooldown = duration;

         state = BeamState.Firing;
         OnBeamStartFiring?.Invoke();

         //Debug.Log($"{name}: Charging finished. Firing beam!");
      }

      if (fireCooldown <= 0.0f && state == BeamState.Firing)
      {
         // Firing finished, cooling down.
         state = BeamState.CoolingDown;
         OnBeamCoolingDown?.Invoke();
         fireFinishTime = Time.time;

         //Debug.Log($"{name}: Firing finished. Cooling down!");
      }

      if (Time.time - fireFinishTime > reloadTime && state == BeamState.CoolingDown)
      {
         state = BeamState.Ready;
         //Debug.Log($"{name}: Cooldown finished. Ready to fire!");
      }
   }

   /// <summary>
   /// Turns to face the target with no limitations. When no target, will go back to starting rotation.
   /// </summary>
   private void TrackTarget()
   {
      if (target != null)
         transform.LookAt(target);
      else
         transform.localRotation = startLocalRot;
   }

   /// <summary>
   /// Move value towards zero but will never overshoot.
   /// </summary>
   private void MoveTowardsZero(ref float value)
   {
      value = Mathf.MoveTowards(value, 0.0f, Time.deltaTime);
   }

   /// <summary>
   /// Manages the raycasting, collider, and visual line rendering of the beam.
   /// Also contains several events relating to the beam impacting something.
   /// </summary>
   private class BeamCannonBeam
   {
      public Transform target;

      private Transform transform;
      private GameObject beam;
      private LineRenderer line;
      private CapsuleCollider collider;

      private float offset = 0.0f;
      private float maxRange = 5000.0f;

      private int hitMask = -1;

      private bool firing = false;

      private const float DEFAULT_LENGTH = 500.0f; // Starting length of the LineRenderer/Collider.
      private const float SCROLL_SPEED = 5.0f;     // How fast the texture scrolls across the LineRenderer.
      private const float MIN_BEAM_DIST = 1.0f;    // Beam raytracing starts this many meters away from the object.

      public event Action<Vector3, Vector3> OnBeamImpacting;
      public event Action OnBeamMissing;

      public BeamCannonBeam(Transform parent, Gradient color, Material lineMaterial, float width, float maxRange, int hitMask)
      {
         this.hitMask = hitMask;
         this.maxRange = maxRange;
         transform = parent;

         beam = new GameObject("Beam");
         beam.transform.SetParent(parent, false);

         // Capsule collider exists to allow "splash damage" when ships fly close to the beam.
         collider = beam.gameObject.AddComponent<CapsuleCollider>();
         collider.direction = 2; // Z-Axis
         collider.radius = width; // Twice the size of the beam itself.
         collider.isTrigger = true;

         // LineRenderer provides the visual for the beam.
         line = beam.gameObject.AddComponent<LineRenderer>();
         //line.startColor = color;
         //line.endColor = color;
         line.colorGradient = color;
         line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
         line.receiveShadows = false;
         line.allowOcclusionWhenDynamic = false;
         line.widthMultiplier = width;
         line.material = lineMaterial;
         line.useWorldSpace = false;
         line.textureMode = LineTextureMode.Tile;
         line.positionCount = 3;

         SetBeamLength(DEFAULT_LENGTH);
         beam.SetActive(false);
      }

      public void Update()
      {
         ScrollTexture();
         UpdateBeamLineAndCollider();
      }

      private void UpdateBeamLineAndCollider()
      {
         if (firing)
         {
            Ray rayToTarget;

            // Ray starts some distance away so that it's guaranteed to start from inside the capsule collider
            // that makes up the beam. This prevents false hits when moving the beam/collider around.
            if (target != null)
               rayToTarget = new Ray(transform.position + (transform.forward * MIN_BEAM_DIST), target.position - transform.position);
            else
               rayToTarget = new Ray(transform.position + (transform.forward * MIN_BEAM_DIST), transform.forward);

            // Fire ray at target. If the target hits, then set the beam and collider length accordingly.
            RaycastHit hitInfo;
            bool hit = Physics.Raycast(rayToTarget, out hitInfo, maxRange, hitMask);

            // If hit something, and didn't hit our own capsule collider.
            if (hit)
            {
               // Hit target. Shorten the length of the line and collider
               float distanceToHit = Vector3.Distance(hitInfo.point, transform.position);
               SetBeamLength(distanceToHit);
               OnBeamImpacting?.Invoke(hitInfo.point, hitInfo.normal);

               //Debug.Log($"Hit {hitInfo.transform.name}");
               Debug.DrawLine(hitInfo.point - Vector3.right * 10.0f, hitInfo.point + Vector3.right * 10.0f);
               Debug.DrawLine(hitInfo.point - Vector3.up * 10.0f, hitInfo.point + Vector3.up * 10.0f);
               Debug.DrawLine(hitInfo.point - Vector3.forward * 10.0f, hitInfo.point + Vector3.forward * 10.0f);
               Debug.DrawRay(rayToTarget.origin, rayToTarget.direction * distanceToHit, Color.red);
            }
            else
            {
               // No hit, reposition capsule collider at max range.
               SetBeamLength(maxRange);
               OnBeamMissing?.Invoke();
               Debug.DrawRay(rayToTarget.origin, rayToTarget.direction * maxRange, Color.blue);
            }
         }
      }

      public void FireAtTarget()
      {
         firing = true;
         UpdateBeamLineAndCollider();
         beam.SetActive(true);
      }

      public void StopFiring()
      {
         firing = false;
         beam.SetActive(false);
      }

      /// <summary>
      /// Scrolling the texture is used to give forward "movement" to the beam while it's active.
      /// </summary>
      private void ScrollTexture()
      {
         offset -= SCROLL_SPEED * Time.deltaTime;

         // Loop it around.
         while (offset < -100.0f)
            offset += 100.0f;

         line.material.mainTextureOffset = new Vector2(offset, 0.0f);
      }

      /// <summary>
      /// Adjusts the length of the line and capsule collider used by the beam.
      /// </summary>
      /// <param name="length"></param>
      private void SetBeamLength(float length)
      {
         // Collider height must be length of raycast. Center must be height/2.
         collider.height = length;
         collider.center = Vector3.forward * (length * 0.5f);

         // Line's element 2's Z needs to be the length of the raycast.
         line.SetPosition(1, Vector3.forward * 6.0f);
         line.SetPosition(2, Vector3.forward * length);
      }
   }

   /// <summary>
   /// Starts and stops the particle systems used by the beam cannon.
   /// </summary>
   private class BeamCannonParticles
   {
      private ParticleSystem chargePSystem;
      private ParticleSystem firePSystem;
      private ParticleSystem impactPSystem;

      private float impactOffset = 2.0f;

      public BeamCannonParticles(Transform transform, ParticleSystem chargePrefab, ParticleSystem firePrefab, ParticleSystem impactPrefab, float impactOffset)
      {
         if (chargePrefab != null)
         {
            chargePSystem = Instantiate(chargePrefab, transform, false);
            DisablePlayOnAwake(chargePSystem);
         }
         else
            Debug.LogError($"{transform.name}: Missing charge effect prefab!");

         if (firePrefab != null)
         {
            firePSystem = Instantiate(firePrefab, transform, false);
            DisablePlayOnAwake(firePSystem);
         }
         else
            Debug.LogError($"{transform.name}: Missing fire effect prefab!");

         if (impactPrefab != null)
         {
            impactPSystem = Instantiate(impactPrefab);
            DisablePlayOnAwake(impactPSystem);
         }
         else
            Debug.LogError($"{transform.name}: Missing impact effect prefab!");

         this.impactOffset = impactOffset;
      }

      public void Charge()
      {
         chargePSystem?.Play(true);
      }

      public void Fire()
      {
         chargePSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
         firePSystem?.Play();
      }
      
      public void Cooldown()
      {
         firePSystem?.Stop();
      }

      public void StartImpacting(Vector3 position, Vector3 normal)
      {
         impactPSystem.transform.position = position + (normal * impactOffset);
         impactPSystem.transform.forward = normal;

         if (!impactPSystem.isPlaying)
         {
            impactPSystem.Play(true);
            //print($"Play impacting effect at {position}!");
         }
      }

      public void StopImpacting()
      {
         impactPSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
         //print("Stopped Impacting!");
      }

      private void DisablePlayOnAwake(ParticleSystem PSystem)
      {
         var main = PSystem.main;
         main.playOnAwake = false;
      }
   }

   /// <summary>
   /// Manages the dynamically created beam cannon audio sources.
   /// </summary>
   private class BeamCannonSound
   {
      // Used for both charging and cooling down.
      private AudioSource chargeSource;
      private AudioSource loopSource;
      private AudioSource shotSource;

      private AudioClip upClip;
      private AudioClip downClip;

      public BeamCannonSound(GameObject soundGameObject, AudioClip up, AudioClip down, AudioClip fireLoop, AudioClip shot)
      {
         // Initialize audio.
         chargeSource = soundGameObject.AddComponent<AudioSource>();
         loopSource = soundGameObject.AddComponent<AudioSource>();
         shotSource = soundGameObject.AddComponent<AudioSource>();

         upClip = up;
         downClip = down;

         InitAudioSource(chargeSource, up, false);
         InitAudioSource(shotSource, shot, false);
         InitAudioSource(loopSource, fireLoop, true);
      }

      public void Charge()
      {
         chargeSource.clip = upClip;
         chargeSource.Play();
      }

      public void Fire()
      {
         loopSource.Play();
         shotSource.Play();
      }

      public void Cooldown()
      {
         loopSource.Stop();
         chargeSource.clip = downClip;
         chargeSource.Play();
      }

      private void InitAudioSource(AudioSource source, AudioClip clip, bool loop)
      {
         source.clip = clip;
         source.playOnAwake = false;
         source.spatialBlend = 1.0f;
         source.dopplerLevel = 0.0f;
         source.rolloffMode = AudioRolloffMode.Linear;
         source.minDistance = 10.0f;
         source.maxDistance = 5000.0f;
         source.loop = loop;
      }
   }
}
