using UnityEngine;

namespace BeamCannonDemo
{
   public class Slider : MonoBehaviour
   {
      public float magnitude;

      private void Update()
      {
         transform.Translate(Vector3.forward * Mathf.Sin(Time.deltaTime) * magnitude * Time.deltaTime, Space.Self);
      }
   }
}
