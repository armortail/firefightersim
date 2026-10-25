using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void PlayGame()
    {
        SceneManager.LoadSceneAsync(1);// just use this logic when you make the pause function
    }
    public void QuitGame()
    {
        SceneManager.UnloadSceneAsync(0);//doing this to make sure it comes back
    }
    public void Keybinds()
    {
        SceneManager.LoadSceneAsync(3);//
    }
}
