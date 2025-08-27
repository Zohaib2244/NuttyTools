using TMPro;
using UnityEngine;

public class DisplayFPS : MonoBehaviour
{
    private float timer, refresh, avgframerate;
    public string Display = "{0} FPS";
    public TextMeshProUGUI FPS_Text;

    private void Update()
    {
        //Application.targetFrameRate = 60;

        float timelspes = Time.smoothDeltaTime;
        timer = timer <= 0 ? refresh : timer -= timelspes;

        if (timer <= 0) avgframerate = (int)(1f / timelspes);
        FPS_Text.text = string.Format(Display, avgframerate.ToString());
       
    }
    private void Start()
    {
        Application.targetFrameRate = 60;
        Debug.Log("Your Application Current FPS = "+avgframerate);
    }
}
