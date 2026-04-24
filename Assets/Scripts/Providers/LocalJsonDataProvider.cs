using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CampusInfoRoot
{
    public string version;
    public string last_updated;
    public string campus;
    public List<CampusBuilding> buildings;
    public List<CampusEvent> public_events;
}

[Serializable]
public class CampusBuilding
{
    public string building_id;
    public string name;
    public List<string> aliases;
    public string campus_area;
    public string category;
    public string description;
    public List<string> common_uses;
    public List<CampusInfoPoint> info_points;
    public List<CampusRoom> rooms;
}

[Serializable]
public class CampusInfoPoint
{
    public string zone_id;
    public string title;
    public string description;
    public string semantic_label;
}

[Serializable]
public class CampusRoom
{
    public string room_id;
    public string room_name;
    public string type;
    public string description;
    public List<string> notes;
}

[Serializable]
public class CampusEvent
{
    public string event_id;
    public string title;
    public string location;
    public string time;
    public string summary;
}

[Serializable]
public class PersonalScheduleRoot
{
    public string version;
    public string last_updated;
    public PersonalProfile profile;
    public List<PersonalScheduleItem> today_schedule;
    public List<PersonalTaskItem> upcoming_tasks;
    public List<DemoContextExample> demo_context_examples;
}

[Serializable]
public class PersonalProfile
{
    public string user_id;
    public string display_name;
    public string home_campus_area;
    public PersonalPreferences preferences;
}

[Serializable]
public class PersonalPreferences
{
    public string language;
    public string interaction_style;
    public bool show_next_class_first;
}

[Serializable]
public class PersonalScheduleItem
{
    public string item_id;
    public string type;
    public string title;
    public string building_id;
    public string room_id;
    public string start_time;
    public string end_time;
    public string summary;
}

[Serializable]
public class PersonalTaskItem
{
    public string task_id;
    public string title;
    public string due_time;
    public string course;
    public string priority;
}

[Serializable]
public class DemoContextExample
{
    public string trigger_zone;
    public string suggested_user_query;
    public string expected_context_summary;
}

public class LocalJsonDataProvider : MonoBehaviour
{
    [SerializeField] private string campusInfoResourcePath = "Data/campus_info";
    [SerializeField] private string personalScheduleResourcePath = "Data/personal_schedule";

    private CampusInfoRoot campusInfo;
    private PersonalScheduleRoot personalSchedule;

    private void Awake()
    {
        LoadAll();
    }

    public void LoadAll()
    {
        TextAsset campusText = Resources.Load<TextAsset>(campusInfoResourcePath);
        TextAsset personalText = Resources.Load<TextAsset>(personalScheduleResourcePath);

        if (campusText == null)
        {
            Debug.LogError("[LocalJsonDataProvider] Could not load campus_info.json");
            return;
        }

        if (personalText == null)
        {
            Debug.LogError("[LocalJsonDataProvider] Could not load personal_schedule.json");
            return;
        }

        campusInfo = JsonUtility.FromJson<CampusInfoRoot>(campusText.text);
        personalSchedule = JsonUtility.FromJson<PersonalScheduleRoot>(personalText.text);

        Debug.Log("[LocalJsonDataProvider] JSON loaded successfully.");
    }

    public string GetBuildingSummary(string buildingNameHint)
    {
        if (campusInfo == null || campusInfo.buildings == null)
            return "No campus building info loaded.";

        foreach (var building in campusInfo.buildings)
        {
            if (building.name == buildingNameHint)
            {
                return building.description;
            }
        }

        return "No matching building found.";
    }

    public string GetNextClassSummary()
    {
        if (personalSchedule == null || personalSchedule.today_schedule == null || personalSchedule.today_schedule.Count == 0)
            return "No personal class info loaded.";

        return personalSchedule.today_schedule[0].summary;
    }

    public string GetUpcomingTaskSummary()
    {
        if (personalSchedule == null || personalSchedule.upcoming_tasks == null || personalSchedule.upcoming_tasks.Count == 0)
            return "No upcoming task loaded.";

        return personalSchedule.upcoming_tasks[0].title + " due at " + personalSchedule.upcoming_tasks[0].due_time;
    }
}