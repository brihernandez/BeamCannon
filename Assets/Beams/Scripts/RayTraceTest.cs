using UnityEngine;

public class RayTraceTest : MonoBehaviour
{
   public Transform target;
   public LayerMask hitMask = -1;
   public Collider otherCollider;

   const float maxRange = 5000.0f;

   private void Update()
   {
      // Face target.
      if (target != null)
         transform.LookAt(target);

      // Raytrace target.
      Ray rayToTarget;

      if (target != null)
         rayToTarget = new Ray(transform.position, target.position - transform.position);
      else
         rayToTarget = new Ray(transform.position, transform.forward);

      // Fire ray at target. If the target hits, then set the beam and collider length accordingly.
      RaycastHit[] hitInfo = Physics.RaycastAll(rayToTarget, maxRange, hitMask);
      RaycastHit closestHit = new RaycastHit();
      float closestDistance = float.MaxValue;

      foreach (RaycastHit hit in hitInfo)
      {
         if (hit.collider != otherCollider)
         {
            // Find closest hit that wasn't our own collider.
            float hitDistance = Vector3.Distance(hit.point, transform.position);
            if (hitDistance < closestDistance)
            {
               closestDistance = hitDistance;
               closestHit = hit;
            }
         }
      }

      if (hitInfo.Length > 0)
      {
         //Debug.Log($"Hit {hitInfo.transform.name}");
         Debug.DrawLine(closestHit.point - Vector3.right * 10.0f, closestHit.point + Vector3.right * 10.0f);
         Debug.DrawLine(closestHit.point - Vector3.up * 10.0f, closestHit.point + Vector3.up * 10.0f);
         Debug.DrawLine(closestHit.point - Vector3.forward * 10.0f, closestHit.point + Vector3.forward * 10.0f);
         Debug.DrawRay(rayToTarget.origin, rayToTarget.direction * closestDistance, Color.red);
      }
      else
      {
         // No hit, reposition capsule collider at max range.
         Debug.DrawRay(rayToTarget.origin, rayToTarget.direction * maxRange, Color.blue);
      }
   }
}