using UnityEngine;
using LightSide;

public class PathAccessorTestRunner : MonoBehaviour
{
    private void Update()
    {
        if (InputUtils.GetMouseButtonDown(0) || InputUtils.GetTouchBegan())
        {
            PathAccessorTests.TestNativeVsCached();
        }
    }
}
