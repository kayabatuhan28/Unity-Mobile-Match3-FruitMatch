using System;
using UnityEngine;

public class EnergyManager : MonoBehaviour
{
    public static EnergyManager Instance;

    public int maxEnergy = 5;
    public int energyPerMinutes = 10;

    private int currentEnergy;
    private DateTime lastEnergyTime;

    const string ENERGY_KEY = "ENERGY";
    const string TIME_KEY = "ENERGY_TIME";

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        Load();
        RefillEnergy();
        Save();
    }

    void RefillEnergy()
    {
        if (currentEnergy >= maxEnergy) return;

        TimeSpan passedTime = DateTime.Now - lastEnergyTime;
        int earnedEnergy = (int)(passedTime.TotalMinutes / energyPerMinutes);

        if (earnedEnergy <= 0) return;

        currentEnergy = Mathf.Min(currentEnergy + earnedEnergy, maxEnergy);

        // Preserve remaining time
        lastEnergyTime = DateTime.Now.AddMinutes(
            -(passedTime.TotalMinutes % energyPerMinutes)
        );
    }

    public bool UseEnergy(int amount = 1)
    {
        if (currentEnergy < amount) return false;

        currentEnergy -= amount;

        if (currentEnergy < maxEnergy)
            lastEnergyTime = DateTime.Now;

        Save();
        return true;
    }

    void Save()
    {
        PlayerPrefs.SetInt(ENERGY_KEY, currentEnergy);
        PlayerPrefs.SetString(TIME_KEY, lastEnergyTime.ToString());
    }

    void Load()
    {
        currentEnergy = PlayerPrefs.GetInt(ENERGY_KEY, maxEnergy);

        string time = PlayerPrefs.GetString(TIME_KEY, DateTime.Now.ToString());
        lastEnergyTime = DateTime.Parse(time);
    }

    // for UI
    public int GetEnergy() => currentEnergy;

    public TimeSpan GetTimeToNextEnergy()
    {
        if (currentEnergy >= maxEnergy)
            return TimeSpan.Zero;

        return lastEnergyTime.AddMinutes(energyPerMinutes) - DateTime.Now;
    }
}
