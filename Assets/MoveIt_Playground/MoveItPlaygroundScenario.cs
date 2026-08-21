using System;

/// <summary>
/// One self-contained situation the engine is put through. The host builds a fresh stage before
/// <see cref="Enter"/> and tears it down after <see cref="Exit"/>, so a scenario never has to clean up
/// objects it spawned through the stage — only whatever it owns itself.
/// </summary>
[Serializable]
public abstract class MoveItPlaygroundScenario
{
    /// <summary>Name shown in the scenario list.</summary>
    public abstract string Title { get; }

    /// <summary>One line telling the viewer what to watch for.</summary>
    public virtual string Watch => string.Empty;

    /// <summary>Whether this scenario exists to break the engine rather than to show it working.</summary>
    public virtual bool IsStress => false;

    /// <summary>Builds the situation. Runs once, with an empty stage.</summary>
    public abstract void Enter(MoveItStage stage);

    /// <summary>Keeps the situation moving. Runs every frame while the scenario is current.</summary>
    public virtual void Tick(MoveItStage stage)
    {
    }

    /// <summary>Releases anything held outside the stage. The stage itself is cleared by the host.</summary>
    public virtual void Exit()
    {
    }
}
