using UnityEngine;
using UnityEngine.SceneManagement;

namespace LightSide
{
    public class SceneLoader : MonoBehaviour
    {
        public int sceneIndex;

        public void Load()
        {
            SceneManager.LoadScene(sceneIndex);
        }
    }
}