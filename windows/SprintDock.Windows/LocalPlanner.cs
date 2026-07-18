namespace SprintDock.Windows;

public static class LocalPlanner
{
    public static SprintState Build(string goal, string details, int focusMinutes)
    {
        var cleanGoal = goal.Trim();
        var cleanDetails = details.Trim();
        var lower = $"{cleanGoal} {cleanDetails}".ToLowerInvariant();
        string[] tasks;
        string finishLine;

        if (ContainsAny(lower, "website", "landing page", "site", "homepage"))
        {
            tasks = [
                "Define the one result this page must create",
                "Finish the highest-impact section",
                "Check the full path on desktop and mobile",
                "Fix the most visible friction",
                "Publish and verify the live result"
            ];
            finishLine = "The page is published, understandable, and its primary action works on desktop and mobile.";
        }
        else if (ContainsAny(lower, "code", "feature", "bug", "app", "build"))
        {
            tasks = [
                "Define the smallest behavior that proves success",
                "Implement the core path",
                "Handle the most likely failure state",
                "Run focused verification",
                "Review and package the result"
            ];
            finishLine = "The useful version works, has been checked, and is ready to use or hand off.";
        }
        else if (ContainsAny(lower, "write", "essay", "article", "report", "draft"))
        {
            tasks = [
                "Define the reader and the main point",
                "Create a short working outline",
                "Write the difficult section first",
                "Tighten structure and clarity",
                "Proofread and prepare the final version"
            ];
            finishLine = "A complete, readable version is saved and ready to submit or share.";
        }
        else if (ContainsAny(lower, "study", "exam", "learn", "homework", "assignment"))
        {
            tasks = [
                "Name the exact material to cover",
                "Review the hardest concept",
                "Test recall without notes",
                "Correct the weakest answers",
                "Write the next review cue"
            ];
            finishLine = "The material can be explained without notes and weak spots are clearly identified.";
        }
        else if (ContainsAny(lower, "launch", "customer", "business", "offer", "sales"))
        {
            tasks = [
                "Clarify the promise and intended customer",
                "Finish the primary launch asset",
                "Test the customer path",
                "Remove the biggest point of confusion",
                "Ship and record the first follow-up"
            ];
            finishLine = "The offer is live, the customer path works, and the next follow-up is scheduled.";
        }
        else
        {
            tasks = [
                "Define what finished means",
                "Choose the smallest useful version",
                "Complete the hardest visible step",
                "Check the result against the finish line",
                "Save, share, or deliver the result"
            ];
            finishLine = string.IsNullOrWhiteSpace(cleanDetails)
                ? "The useful version is complete, checked, and ready to use or share."
                : cleanDetails;
        }

        var state = new SprintState
        {
            Goal = cleanGoal,
            FinishLine = finishLine,
            NextAction = tasks[0],
            FocusMinutes = focusMinutes,
            RemainingSeconds = focusMinutes * 60
        };

        for (var index = 0; index < tasks.Length; index++)
        {
            state.Tasks.Add(new SprintTask { Title = tasks[index], IsCurrent = index == 0 });
        }

        state.Checklist.Add(new ChecklistItem { Group = "Finish", Title = "Finish line reached" });
        state.Checklist.Add(new ChecklistItem { Group = "Review", Title = "Result checked" });
        state.Checklist.Add(new ChecklistItem { Group = "Ship", Title = "Work saved or shared" });
        state.Activity.Add(new ActivityItem { Message = $"Started sprint: {cleanGoal}" });
        return state;
    }

    private static bool ContainsAny(string value, params string[] terms) => terms.Any(value.Contains);
}
