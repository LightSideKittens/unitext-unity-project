using UnityEngine;

public sealed class RandomRotator : MonoBehaviour
{
    [SerializeField, Min(0f)] private float interval = 1f;
    [SerializeField] private Space space = Space.Self;

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < interval)
            return;

        timer -= interval;

        Quaternion rotation = Random.rotation;
        if (space == Space.Self)
            transform.localRotation = rotation;
        else
            transform.rotation = rotation;
    }
}
