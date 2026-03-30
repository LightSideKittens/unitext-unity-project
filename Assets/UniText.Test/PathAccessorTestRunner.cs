using UnityEngine;

public class PathAccessorTestRunner : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            PathAccessorTests.TestNativeVsCached();
        }
    }
}
