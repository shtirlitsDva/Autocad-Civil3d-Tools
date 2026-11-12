# SETMIDTLABELSET

Replaces the existing profile label groups of all displayed MIDT profiles in the current drawing's profile views with one selected profile label set.

1. Run `SETMIDTLABELSET`.
2. Read the numbered list of profile label sets already in the drawing.
3. Enter the number of the desired set. Escape cancels before any changes.

The command processes each displayed MIDT profile/view pair separately, including multiple views of the same profile. MIDT is matched as a case-insensitive name token separated by spaces, underscores or hyphens, for example `004 MIDT`. Hidden profiles are skipped.

Existing groups for those pairs are erased before Civil 3D's native profile-override label-set importer runs. Repeating the command replaces labels without accumulating groups. Choosing an empty set such as `_No Labels` removes the groups. All replacements share one transaction; an import failure rolls them back together. The command reports the profile, view and group counts.

Individual labels outside profile label groups and profile-view band labels are outside this command's scope. It does not edit profile geometry. MYPFP's creation defaults are unchanged.

The command lives in the existing IntersectUtilities assembly. Reload the rebuilt library before first use. Native COM interfaces are embedded at build time; no separate command assembly or copied Civil 3D interop DLL is required.
