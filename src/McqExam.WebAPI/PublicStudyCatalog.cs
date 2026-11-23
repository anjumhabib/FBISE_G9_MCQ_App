using McqExam.Application;

namespace McqExam.WebAPI;

public sealed record StudyTopic(string Heading, string Text);

public sealed record StudySubject(string Slug, string Code, string Name, string Description,
    string ImageAlt, IReadOnlyList<StudyTopic> Tips)
{
    public string Image => $"/images/public/subject-{Slug}.svg";
}

public static class PublicStudyCatalog
{
    public const string Frameworks = "https://www.fbise.edu.pk/curriculum_model_paper.php";

    public static IReadOnlyList<StudySubject> Subjects { get; } = Array.AsReadOnly<StudySubject>(
    [
        new("biology", "BIO", "Biology", "Study cells, biological processes, and the relationships between structure and function.",
            "A simplified plant cell with wall, nucleus, and chloroplasts beside a DNA helix",
            [
                new("Draw, label, explain", "Sketch a plant cell without looking at your notes. Label the wall, membrane, central vacuole and chloroplasts, then give one function for each. Compare the sketch with an animal cell and name a structure both share."),
                new("Trace a process", "For osmosis, identify the membrane and the direction water moves. For enzymes, predict what happens as temperature rises beyond the optimum. Explain each prediction in a sentence before looking at options."),
                new("Read the diagram first", "Identify which part an arrow touches before reading its proposed labels. For experiments, identify the variable changed, the outcome measured, and at least one condition held constant.")
            ]),
        new("chemistry", "CHEM", "Chemistry", "Connect atomic structure, chemical formulas, reactions, and laboratory observations.",
            "A schematic atom and a flask with a marked liquid level",
            [
                new("Keep coefficients and subscripts separate", "Count each element before balancing an equation. Change coefficients, then count again. Changing H₂O to H₂O₂ changes the substance."),
                new("Move from particles to observations", "Use electron arrangement to reason about ions and bonding. When reading a laboratory question, distinguish what was seen, such as a gas forming, from the explanation proposed for it."),
                new("Check the full formula", "Read chemical symbols with their correct capitalization and check that charges balance when writing an ionic compound. For a numerical problem, write the quantity and unit together at each step.")
            ]),
        new("physics", "PHY", "Physics", "Practise measurement, motion, forces, energy, and interpreting graphs.",
            "A position–time graph whose upward slope shows steady motion",
            [
                new("Write the known quantities", "Convert to compatible SI units before using an equation. For constant speed, 12 metres in 4 seconds gives 3 metres per second. Check that the result has the correct unit and a reasonable size."),
                new("Read both axes", "On a position–time graph, slope gives velocity. On a velocity–time graph, slope gives acceleration and the signed area gives displacement. Name the axes before choosing a rule."),
                new("State the direction", "Draw arrows for forces and establish a positive direction. Equal forces acting in opposite directions give zero net force, which allows an object to keep moving at constant velocity.")
            ]),
        new("mathematics", "MATH", "Mathematics", "Build fluency in algebra, geometry, graphs, and reasoning from definitions.",
            "Coordinate axes with a right triangle and labelled rise and run",
            [
                new("Work an example before reading the choices", "Write a short line of working first. For an exponent rule, test your reasoning with a simple base. Keep track of signs and restrictions such as nonzero denominators."),
                new("Check geometry assumptions", "Mark equal sides, parallel lines, and right angles only where given or proved. Write down the theorem and its conditions before applying it to a diagram."),
                new("Verify by substitution", "After solving an equation, substitute the answer into the original expression. For an inequality, remember that multiplication or division by a negative number reverses the inequality.")
            ]),
        new("computer-science", "CS", "Computer Science", "Explore data representation, algorithms, programming logic, and computer systems.",
            "A two-input AND gate beside a decision step in a flowchart",
            [
                new("Dry-run algorithms on paper", "Use a trace table with one row per iteration. Record the loop condition, variable values and output. Try an empty input or a boundary value to expose an off-by-one error."),
                new("Work through binary and logic", "Write place values above binary digits before converting. For a logic gate, test all input combinations and label the intermediate outputs before deciding the final result."),
                new("Separate purpose from implementation", "Explain what a system does before naming its components. Compare algorithms with the same goal by tracing them on the same input and noting where they differ.")
            ])
    ]);

    public static StudySubject? Find(string slug) =>
        Subjects.FirstOrDefault(subject => subject.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
}

public sealed record PublicHome(int Subjects, int Chapters, int ChaptersWithDiagrams);
public sealed record StudyGuide(StudySubject Subject, IReadOnlyList<ChapterInfo> Chapters, IReadOnlyList<BloomCoverage> Bloom);
