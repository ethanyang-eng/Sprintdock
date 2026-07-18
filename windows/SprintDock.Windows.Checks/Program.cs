using System.Text.Json;
using SprintDock.Windows;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var sitePlan = LocalPlanner.Build("Publish my portfolio website", "Mobile ready", 25);
Require(sitePlan.Tasks.Count is >= 3 and <= 5, "Planner must return a compact plan.");
Require(sitePlan.Tasks.Count(task => task.IsCurrent) == 1, "Planner must select one current step.");
Require(sitePlan.Tasks[0].Title == sitePlan.NextAction, "First step must be the next action.");
Require(sitePlan.FocusMinutes == 25 && sitePlan.RemainingSeconds == 1500, "Timer seed is incorrect.");

var writingPlan = LocalPlanner.Build("Write the final report", "Ready to submit", 45);
Require(writingPlan.Tasks.Any(task => task.Title.Contains("outline", StringComparison.OrdinalIgnoreCase)),
    "Writing goals should receive a writing-specific plan.");

var json = JsonSerializer.Serialize(sitePlan);
var restored = JsonSerializer.Deserialize<SprintState>(json);
Require(restored?.Goal == sitePlan.Goal, "Sprint state did not round-trip.");
Require(restored?.Tasks.Count == sitePlan.Tasks.Count, "Task state did not round-trip.");

Console.WriteLine("Sprint Dock Windows checks passed.");
