# Language tables

`Seety/Localization/Strings/Strings*.cs` are **generated**. Do not edit them by hand.

Every language is emitted from one table in this folder, so a key cannot exist in one language
and be missing from another - which is the failure that makes a mod look half-translated.

```bash
cd tools/locale && python loc_emit.py
```

- `gen_locale.py` - the locale list and the options-page strings
- `loc_vitals.py` - one title and one short label per vital, keyed on the vital's own id
- `loc_ui.py` - everything the UI draws itself: headers, buttons, empty states, tooltips
- `loc_emit.py` - writes the C# tables and the registry

Adding a language means adding it to `LOCALES` and one string to every entry. Adding a string
means one line per language in the same place.
