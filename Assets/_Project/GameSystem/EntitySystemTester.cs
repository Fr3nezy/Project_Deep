using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tool per testare Entity System (Health + Stamina).
/// </summary>
public class EntitySystemTester : MonoBehaviour
{
    [Header("Test Target")]
    public Entity testEntity;
    
    [Header("Test Settings")]
    public float damageAmount = 3f;
    public float healAmount = 2f;

    void Update()
    {
        if (testEntity == null) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // ===== HEALTH TESTS =====
        
        // 1 = Damage
        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            testEntity.TakeDamage(damageAmount, null);
            Debug.Log($"⚔️ Inflitto {damageAmount} danno a {testEntity.GetEntityName()}");
        }

        // 2 = Heal
        if (keyboard.digit2Key.wasPressedThisFrame)
        {
            testEntity.Heal(healAmount);
            Debug.Log($"💚 Curato {healAmount} HP a {testEntity.GetEntityName()}");
        }

        // 3 = Status
        if (keyboard.digit3Key.wasPressedThisFrame)
        {
            LogFullStatus();
        }

        // 4 = Kill
        if (keyboard.digit4Key.wasPressedThisFrame)
        {
            if (testEntity.HasHealth())
            {
                testEntity.TakeDamage(9999f, null);
                Debug.Log($"☠️ Kill command su {testEntity.GetEntityName()}");
            }
        }

        // ===== STAMINA TESTS =====
        
        // 5 = Sprint (tieni premuto)
        if (keyboard.digit5Key.isPressed)
        {
            if (testEntity.HasStamina())
            {
                bool success = testEntity.GetStamina().UseSprint(Time.deltaTime);
                if (!success && keyboard.digit5Key.wasPressedThisFrame)
                {
                    Debug.Log("❌ Cannot sprint (exhausted or no stamina)");
                }
            }
        }
        else if (keyboard.digit5Key.wasReleasedThisFrame)
        {
            if (testEntity.HasStamina())
            {
                testEntity.GetStamina().StopUsingStamina();
                Debug.Log("🏃 Sprint stopped - stamina will regen");
            }
        }

        // 6 = Speed Multiplier Check
        if (keyboard.digit6Key.wasPressedThisFrame)
        {
            if (testEntity.HasStamina())
            {
                var s = testEntity.GetStamina();
                Debug.Log($"⚡ Speed Multiplier: x{s.GetSpeedMultiplier():F2}" +
                         $"\n  State: {s.GetCurrentState()}" +
                         $"\n  Stamina: {s.GetCurrentStamina():F0}/{s.GetMaxStamina():F0}" +
                         $"\n  Can Sprint: {s.CanUseSprint()}");
            }
        }

        // 7 = Restore Stamina
        if (keyboard.digit7Key.wasPressedThisFrame)
        {
            if (testEntity.HasStamina())
            {
                testEntity.GetStamina().RestoreStamina(50f);
                Debug.Log("💚 Restored 50 stamina");
            }
        }

        // 8 = Reset Stamina
        if (keyboard.digit8Key.wasPressedThisFrame)
        {
            if (testEntity.HasStamina())
            {
                testEntity.GetStamina().ResetStamina();
                Debug.Log("🔄 Stamina reset to full");
            }
        }
    }

    private void LogFullStatus()
    {
        string status = $"📊 {testEntity.GetEntityName()} Full Status:" +
                       $"\n  Type: {testEntity.GetEntityType()}" +
                       $"\n  Alive: {testEntity.IsAlive()}";
        
        if (testEntity.HasHealth())
        {
            var h = testEntity.GetHealth();
            status += $"\n  HP: {h.GetCurrentHealth():F0}/{h.GetMaxHealth():F0} ({h.GetHealthPercentage() * 100:F1}%)";
        }
        
        if (testEntity.HasStamina())
        {
            var s = testEntity.GetStamina();
            status += $"\n  Stamina: {s.GetCurrentStamina():F0}/{s.GetMaxStamina():F0} ({s.GetStaminaPercentage() * 100:F1}%)";
            status += $"\n  State: {s.GetCurrentState()}";
            status += $"\n  Speed Mult: x{s.GetSpeedMultiplier():F2}";
        }
        
        Debug.Log(status);
    }

    void OnGUI()
    {
        if (testEntity == null) return;

        GUI.Box(new Rect(10, 10, 220, 230), "Entity System Tester");
        
        // Health
        GUI.Label(new Rect(20, 35, 200, 20), "=== HEALTH ===");
        GUI.Label(new Rect(20, 55, 200, 20), "1 = Damage (-3 HP)");
        GUI.Label(new Rect(20, 75, 200, 20), "2 = Heal (+2 HP)");
        GUI.Label(new Rect(20, 95, 200, 20), "3 = Full Status");
        GUI.Label(new Rect(20, 115, 200, 20), "4 = Kill");
        
        // Stamina
        GUI.Label(new Rect(20, 140, 200, 20), "=== STAMINA ===");
        GUI.Label(new Rect(20, 160, 200, 20), "5 = Sprint (HOLD)");
        GUI.Label(new Rect(20, 180, 200, 20), "6 = Speed Check");
        GUI.Label(new Rect(20, 200, 200, 20), "7 = +50 Stamina");
        GUI.Label(new Rect(20, 220, 200, 20), "8 = Reset Stamina");
    }
}
