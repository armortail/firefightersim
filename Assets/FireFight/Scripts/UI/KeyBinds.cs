using UnityEngine;
using UnityEngine.SceneManagement;

namespace FireFight
{
    public class KeyBinds : MonoBehaviour
    {
        public void ReturnToMain()
        {
            SceneManager.LoadSceneAsync(0);
        }
    }
}
