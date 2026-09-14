namespace Sem.Ui.Services;

/// <summary>
/// What the reader has narrowed the empire lists down to.
/// </summary>
/// <remarks>
/// The empires' own <see cref="Sifter{TRow}"/>, which is where all of the narrowing lives - any
/// within a heading, all across them, and a switch for the headings that can hold several. All this
/// adds is which headings it answers for and what the search box reads, so the pages and controls
/// that pass one around keep the short name they have always used.
/// </remarks>
public sealed class EmpireFilter() : Sifter<EmpireRow>(EmpireFacet.All, row => row.Text);
