"""Import the FBISE MCQ bank into an EF-migrated SQLite database."""
import argparse
import csv
import hashlib
import html
import io
import re
import sqlite3
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path

SUBJECTS = {
    "biology": "BIO", "chemistry": "CHEM", "physics": "PHY",
    "mathematics": "MATH", "computer science": "CS",
}
BLOOM = {
    "remembering": "Knowledge", "knowledge": "Knowledge",
    "understanding": "Conceptual", "conceptual": "Conceptual",
    "applying": "Application", "application": "Application", "analyzing": "Analyzing",
    "analysing": "Analyzing", "remember": "Knowledge", "understand": "Conceptual",
    "apply": "Application", "analyze": "Analyzing", "analyse": "Analyzing",
}
CHAPTER = re.compile(
    r"^(?:(?:class\s+9(?:th)?\s+)?mathematics\W+)?(?:chapter|unit)\s*[-:#.–—]*"
    r"\s*(\d{1,2})\b\s*[-:.–—]*\s*(.*)$", re.I,
)
NUMBER = re.compile(r"^\s*(\d+)[.)]?\s*$")
KEY_HEADING = re.compile(r"^(?:mcqs?\s+)?answers?\s*keys?\b", re.I)
SUBSCRIPT = str.maketrans("₀₁₂₃₄₅₆₇₈₉₊₋", "0123456789+-")
SUPERSCRIPT = str.maketrans("⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻", "0123456789+-")


def clean(value):
    return re.sub(r"\s+", " ", (value or "").replace("\u00ad", "").replace("\ufffe", "-")).strip()


def markup(value):
    escaped = html.escape(value, quote=False)
    escaped = re.sub(r"[₀₁₂₃₄₅₆₇₈₉₊₋]+", lambda m: f"<sub>{m[0].translate(SUBSCRIPT)}</sub>", escaped)
    return re.sub(r"[⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻]+", lambda m: f"<sup>{m[0].translate(SUPERSCRIPT)}</sup>", escaped)


@dataclass
class Item:
    page: int
    content: list[str]
    diagram: bytes | None = None


@dataclass
class Section:
    title: str
    questions: dict[int, Item] = field(default_factory=dict)
    keys: dict[int, tuple[str, str]] = field(default_factory=dict)
    conflicts: set[int] = field(default_factory=set)


def subject_heading(line):
    label = re.sub(r"^(?:subject\s*[:–-]\s*|mcqs?\s+(?:bank|of)\s*)", "", line.casefold())
    label = re.sub(r"\s*(?:[-–:—]?\s*(?:mcqs?(?:\s+bank)?|(?:class\s*)?(?:9(?:th)?|ix)))+$", "", label)
    return SUBJECTS.get(label.strip(" :—–-"))


def tables_for(page, fitz, pdfplumber):
    """Use ruled tables first and fall back to a second extractor for fragmented rules."""
    tables = page.find_tables(paths=page.get_drawings()).tables
    if tables:
        result = []
        for table in tables:
            rows = [(text, row.bbox, row.cells) for text, row in zip(table.extract(), table.rows)]
            result.append((table.bbox, rows))
        return result
    if not re.search(r"(?im)^\s*(?:s\s*#|answer\s*key|bloom.s level)\s*$", page.get_text()):
        return []
    with fitz.open() as single_page:
        single_page.insert_pdf(page.parent, from_page=page.number, to_page=page.number)
        with pdfplumber.open(io.BytesIO(single_page.tobytes())) as fallback:
            return [(table.bbox, [(text, row.bbox, row.cells) for text, row in zip(table.extract(), table.rows)])
                    for table in fallback.pages[0].find_tables()]


def question_columns(rows, left):
    """Locate the six logical columns even when a wrapped heading creates extra table cells."""
    for index, (cells, _, rectangles) in enumerate(rows[:8]):
        labels = [clean(cell).casefold() for cell in cells]
        if "question" not in labels or not all(f"({letter})" in labels for letter in "abcd"):
            continue
        columns = [rectangles[labels.index(label)] for label in ("question", "(a)", "(b)", "(c)", "(d)")]
        if any(rectangle is None for rectangle in columns):
            continue
        number_column = (left + columns[0][0]) / 2
        return index, [number_column, *((rect[0] + rect[2]) / 2 for rect in columns)]
    return None


def column_values(cells, rectangles, centers):
    values = []
    for center in centers:
        value = next((text for text, rect in zip(cells, rectangles) if rect and rect[0] <= center < rect[2]), "")
        values.append(value)
    return values


def key_groups(cells):
    """Discard layout-only cells and keep each numbered answer separate."""
    tokens = [cell for cell in cells if cell]
    offsets = [index for index, token in enumerate(tokens) if NUMBER.fullmatch(token)]
    for start, end in zip(offsets, [*offsets[1:], len(tokens)]):
        number = int(NUMBER.fullmatch(tokens[start])[1])
        option, bloom = tokens[start + 1:end] if end - start == 3 else ("", "")
        yield number, option, bloom


def diagram_for(page, row_box, images, fitz):
    row = fitz.Rect(row_box)
    regions = []
    for image in images:
        box = fitz.Rect(image["bbox"])
        if box.width < 15 or box.height < 12 or box.width > page.rect.width * .8:
            continue
        center = fitz.Point((box.x0 + box.x1) / 2, (box.y0 + box.y1) / 2)
        if row.contains(center):
            regions.append(box)
    if not regions:
        return None
    combined = regions[0]
    for region in regions[1:]:
        combined |= region
    return page.get_pixmap(matrix=fitz.Matrix(2, 2), clip=combined, alpha=False).tobytes("png")


def parse(pdf):
    import pymupdf as fitz
    import pdfplumber

    sections = {}
    report = []
    subject = None
    chapter = None
    key_mode = False
    last_item = None
    titles = {}
    with fitz.open(pdf) as document:
        for index, page in enumerate(document):
            if index and index % 50 == 0:
                print(f"Read {index} of {len(document)} pages", flush=True)
            label_lines = [clean(line) for line in page.get_text().splitlines()]
            if not any(label_lines):
                report.append((subject, chapter, None, index + 1, "Page has no extractable text"))
                continue
            if "contents" in {line.casefold() for line in label_lines[:12]}:
                nonempty = [line for line in label_lines if line]
                for line, following in zip(nonempty, nonempty[1:]):
                    if (entry := CHAPTER.match(line)) and subject:
                        title = entry[2] or (following if not re.match(r"^\d", following) else "")
                        if title:
                            titles[subject, int(entry[1])] = title
                continue
            tables = tables_for(page, fitz, pdfplumber)
            events = [(box[1], 1, (box, rows)) for box, rows in tables]
            for block in page.get_text("dict", flags=fitz.TEXTFLAGS_TEXT)["blocks"]:
                for line in block.get("lines", []):
                    box = fitz.Rect(line["bbox"])
                    if any(fitz.Rect(table_box).contains(box.tl + (1, 1)) for table_box, _ in tables):
                        continue
                    label = clean("".join(span["text"] for span in line["spans"]))
                    events.append((box.y0, 0, label))
            images = page.get_image_info() if tables else []
            untitled = False
            for _, kind, content in sorted(events, key=lambda event: event[:2]):
                if kind == 0:
                    detected = subject_heading(content) if not tables or chapter is None else None
                    if detected and detected != subject:
                        subject, chapter, key_mode = detected, None, False
                        last_item = None
                    elif match := CHAPTER.match(content):
                        number, title = int(match[1]), clean(match[2])
                        if subject:
                            if chapter != number:
                                key_mode = False
                                last_item = None
                            chapter = number
                            if re.search(r"\banswers?\s+keys?\b", title, re.I):
                                key_mode = True
                            title = titles.get((subject, number), title)
                            title = re.sub(r"\s*[-–—]\s*\d+\s+mcqs?\s*$", "", title, flags=re.I)
                            section = sections.setdefault((subject, number), Section(title or f"Chapter {number}"))
                            if title and section.title == f"Chapter {number}":
                                section.title = title
                            untitled = not title and section.title == f"Chapter {number}"
                    elif KEY_HEADING.match(content):
                        key_mode = True
                        untitled = False
                    elif untitled and re.search(r"[A-Za-z]", content):
                        sections[subject, chapter].title = content.title() if content.isupper() else content
                        untitled = False
                    continue

                untitled = False
                table_box, rows = content
                columns = question_columns(rows, table_box[0])
                if columns:
                    header, centers = columns
                    rows = rows[header + 1:]
                    key_mode = False
                elif any("bloom" in label and "correct" in label for label in (
                        " ".join(clean(cell) for cell in cells).casefold() for cells, _, _ in rows[:8])):
                    key_mode = True
                for cells, box, rectangles in rows:
                    cells = [clean(cell) for cell in cells]
                    if not cells:
                        continue
                    if key_mode or len(cells) == 3:
                        for number, option, bloom in key_groups(cells):
                            add_key(sections, report, subject, chapter, number, index + 1, option, bloom)
                        continue
                    if columns:
                        cells = column_values(cells, rectangles, centers)
                    if not (number_match := NUMBER.fullmatch(cells[0])):
                        if not cells[0] and len(cells) == 6 and any(cells[1:]) and last_item is not None:
                            last_item.content = [clean(f"{old} {new}") for old, new in zip(last_item.content, cells[1:])]
                            last_item.diagram = last_item.diagram or diagram_for(page, box, images, fitz)
                        continue
                    number = int(number_match[1])
                    if (subject, chapter) not in sections:
                        report.append((subject, chapter, number, index + 1, "Question or key row outside a recognized chapter"))
                        continue
                    section = sections[subject, chapter]
                    if len(cells) < 6:
                        report.append((subject, chapter, number, index + 1, "Malformed question table row"))
                        continue
                    content_cells = cells[1:6]
                    item = Item(index + 1, content_cells, diagram_for(page, box, images, fitz))
                    if number in section.questions and section.questions[number].content != content_cells:
                        section.conflicts.add(number)
                        report.append((subject, chapter, number, index + 1, "Conflicting question rows"))
                    section.questions[number] = item
                    last_item = item
    return sections, report


def add_key(sections, report, subject, chapter, number, page, option_text, bloom_text):
    if (subject, chapter) not in sections:
        report.append((subject, chapter, number, page, "Answer key outside a recognized chapter"))
        return
    section = sections[subject, chapter]
    option_text = "".join(char for char in unicodedata.normalize("NFKD", option_text)
                          if not unicodedata.combining(char))
    option = re.match(r"^\(?([A-D])(?:\b|\))", option_text, re.I)
    label = re.sub(r"\s*\([A-Z]\)\s*$", "", bloom_text, flags=re.I)
    bloom = BLOOM.get(label.casefold().strip(" ."))
    if not option or not bloom:
        report.append((subject, chapter, number, page, "Invalid correct option or Bloom level"))
        return
    key = option[1].upper(), bloom
    if number in section.keys and section.keys[number] != key:
        section.conflicts.add(number)
        report.append((subject, chapter, number, page, "Conflicting answer keys"))
    section.keys[number] = key


def import_sections(db, diagrams, sections, report):
    count = 0
    with sqlite3.connect(db, timeout=5) as connection:
        connection.execute("PRAGMA foreign_keys=ON")
        connection.execute("PRAGMA journal_mode=WAL")
        connection.execute("PRAGMA busy_timeout=5000")
        connection.execute("BEGIN IMMEDIATE")
        subjects = dict(connection.execute("SELECT Code, Id FROM Subjects"))
        for (code, number), section in sections.items():
            connection.execute(
                """INSERT INTO Chapters (SubjectId, ChapterNo, Title) VALUES (?, ?, ?)
                   ON CONFLICT(SubjectId, ChapterNo) DO UPDATE SET Title=excluded.Title""",
                (subjects[code], number, section.title),
            )
            chapter_id = connection.execute(
                "SELECT Id FROM Chapters WHERE SubjectId=? AND ChapterNo=?", (subjects[code], number)
            ).fetchone()[0]
            for question_no, item in section.questions.items():
                issues = []
                if not item.content[0] or "\ufffd" in " ".join(item.content):
                    issues.append("Missing or malformed question text")
                if any(not cell for cell in item.content[1:]):
                    issues.append("Missing option")
                if question_no not in section.keys:
                    issues.append("Missing answer key")
                report.extend((code, number, question_no, item.page, issue) for issue in issues)
                if issues or question_no in section.conflicts:
                    continue
                image_path = None
                if item.diagram:
                    digest = hashlib.sha256(item.diagram).hexdigest()[:12]
                    filename = f"{code.lower()}_ch{number:02}_q{question_no}_{digest}.png"
                    diagrams.mkdir(parents=True, exist_ok=True)
                    (diagrams / filename).write_bytes(item.diagram)
                    image_path = f"/images/diagrams/{filename}"
                connection.execute(
                    """INSERT INTO Questions
                       (ChapterId, QNo, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectOption,
                        BloomLevel, HasDiagram, ImagePath) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                       ON CONFLICT(ChapterId, QNo) DO UPDATE SET
                         QuestionText=excluded.QuestionText, OptionA=excluded.OptionA,
                         OptionB=excluded.OptionB, OptionC=excluded.OptionC, OptionD=excluded.OptionD,
                         CorrectOption=excluded.CorrectOption, BloomLevel=excluded.BloomLevel,
                         HasDiagram=excluded.HasDiagram, ImagePath=excluded.ImagePath
                       WHERE NOT EXISTS (SELECT 1 FROM ExamAnswers WHERE QuestionId=Questions.Id)""",
                    (chapter_id, question_no, *map(markup, item.content), *section.keys[question_no],
                     bool(image_path), image_path),
                )
                count += 1
            for question_no in sorted(section.keys.keys() - section.questions.keys()):
                report.append((code, number, question_no, None, "Answer key without a question"))
    return count


def main():
    tools = Path(__file__).resolve().parent
    api = tools.parent / "src" / "McqExam.WebAPI"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pdf", type=Path, default=tools / "MCQs Bank Class 9th.pdf")
    parser.add_argument("--db", type=Path, default=api / "App_Data" / "mcq_bank.db")
    parser.add_argument("--diagrams", type=Path, default=api / "wwwroot" / "images" / "diagrams")
    parser.add_argument("--report", type=Path, default=tools / "validation_report.csv")
    args = parser.parse_args()
    if not args.pdf.is_file():
        parser.error(f"PDF file not found: {args.pdf}")
    if not args.db.is_file():
        parser.error(f"Database not found: {args.db}. Apply the EF migrations first.")
    sections, report = parse(args.pdf)
    missing = set(SUBJECTS.values()) - {code for (code, _), section in sections.items()
                                        if section.questions}
    report.extend((code, None, None, None, "No questions detected for subject") for code in sorted(missing))
    count = import_sections(args.db, args.diagrams, sections, report)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    with args.report.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.writer(file)
        writer.writerow(("subject", "chapter", "question_number", "pdf_page", "issue"))
        writer.writerows(report)
    print(f"Processed {count} valid questions across {len(sections)} chapters. {len(report)} issues in {args.report}")
    return 0 if count and not report else 1


if __name__ == "__main__":
    raise SystemExit(main())
