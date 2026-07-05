# Migrating to 2.0

Starting with the version 2.0.0, layout-specific themes are no longer supported. Instead, you can choose a theme in the context menu that will be applied to all toolbars.

<img src="pics/themes.png" alt="Themes" width="300"/>

This means, the layout structure has changed a little bit.

- Layout properties `external_theme` and `theme` no longer work.
- Theme properties `button_size`, `margin`, `min_opacity`, `max_opacity` have been moved directly to layout yaml. Themes can no longer influence these properties.

An example of 1.0 to 2.0 migration for a layout:

```yaml
# old layout
layout_width: 4
external_theme: files/themes/default.yaml
theme:
	button_size: 14

buttons:
	hide:
		action: a
		text: "a"

# new layout
layout_width: 4
button_size: 14
margin: 8

buttons:
	hide:
		action: a
		text: "a"

```

Note that unmodified 1.0 layouts and themes will still function but may get rendered differently.
