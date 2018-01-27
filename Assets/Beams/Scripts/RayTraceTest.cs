using UnityEngine;

public class RayTraceTest : MonoBehaviour
{
   public Transform target;
   public LayerMask hitMask = -1;

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
      RaycastHit hitInfo;
      bool hit = Physics.Raycast(rayToTarget, out hitInfo, maxRange, hitMask);

      // If hit something, and didn't hit our own capsule collider.
      if (hit)
      {
         // Hit target. Shorten the length of the line and collider
         float distanceToHit = Vector3.Distance(hitInfo.point, transform.position);

         //Debug.Log($"Hit {hitInfo.transform.name}");
         Debug.DrawLine(hitInfo.point - Vector3.right * 10.0f, hitInfo.point + Vector3.right * 10.0f);
         Debug.DrawLine(hitInfo.point - Vector3.up * 10.0f, hitInfo.point + Vector3.up * 10.0f);
         Debug.DrawLine(hitInfo.point - Vector3.forward * 10.0f, hitInfo.point + Vector3.forward * 10.0f);
         Debug.DrawRay(rayToTarget.origin, rayToTarget.direction * distanceToHit, Color.red);
      }
      else
      {
         // No hit, reposition capsule collider at max range.
         Debug.DrawRay(rayToTarget.origin, rayToTarget.direction * maxRange, Color.blue);
      }
   }
}