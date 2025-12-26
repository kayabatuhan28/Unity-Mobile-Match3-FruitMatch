using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI currentEnergyText;
    [SerializeField] TextMeshProUGUI maxEnergyText;
    [SerializeField] TextMeshProUGUI goldText;

   
    void Start()
    {  
        if (!PlayerPrefs.HasKey("Level"))
        {
            PlayerPrefs.SetInt("Level", 1);          
            PlayerPrefs.SetInt("ShuffleCount", 3);
            PlayerPrefs.SetInt("SpecialPowerCount", 3);
            PlayerPrefs.SetInt("Gold", 50);
        }

        currentEnergyText.text = EnergyManager.Instance.GetEnergy().ToString();
        maxEnergyText.text = EnergyManager.Instance.maxEnergy.ToString();
        goldText.text = PlayerPrefs.GetInt("Gold").ToString();
    }

    public void OnButtonClick(string ClickedButton)
    {
        if (ClickedButton == "Play")
        {
            if (!EnergyManager.Instance.UseEnergy())
            {
                // Not enough energy             
                return;
            }
            SceneManager.LoadScene(PlayerPrefs.GetInt("Level"));         
        }
        else if (ClickedButton == "Quit")
        {
            Application.Quit();
        }
    }

  

   
}
