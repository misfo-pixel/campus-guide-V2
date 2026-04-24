using UnityEngine;

public class CampusInfoDataBase : MonoBehaviour
{
    [SerializeField] private string buildingName = "Keller Hall";
    [SerializeField] private string roomName = "3-180";
    [SerializeField] private string eventTitle = "CSCI Lecture";
    [SerializeField] private string eventTime = "2:00 PM";
    [SerializeField][TextArea] private string note = "This room is often used for computer science classes.";

    public string GetCampusInfoSummary(string buildingHint)
    {
        if (buildingHint == buildingName)
        {
            return eventTitle + " at " + eventTime + " in room " + roomName + ". " + note;
        }

        return "No matching campus info found.";
    }
}
