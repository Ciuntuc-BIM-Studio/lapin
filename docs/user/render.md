---
title: Render
order: 72
summary: Render the 3D view as a photograph, light traced as it bounces, and save it as an image of any size.
---

# Render

The 3D view can be **rendered**: the light is traced as it really travels, from the sun, the sky and every lamp, bouncing off walls, floors and ceilings and through glass, until the picture looks like a photograph. Lapin renders exactly what the view shows (the same elements, materials and textures, the same cut by a section or the section box, the same storeys and hidden elements), with the view's own sky and sun on the project's day and hour, and with its exposure and white balance.

Rendering needs the **Medium** or **Realistic** view (View → **Display quality**, see [Realistic, with the sun](views.md#realistic-with-the-sun)).

## Rendering the view

Press **Render** in the lower right corner of the 3D view (or View → **Render**). The picture starts grainy and clears sample by sample while the camera rests; turn, zoom or move and it starts again, the live view showing while the camera moves. The chip at the bottom says how far it has got (*128 samples · 0:42*); it stops when the picture is done (by default after 256 samples). **×** brings back the live view.

![A brick house at noon, rendered: the sky's light, the sun's soft shadows, the brick's relief](shots/render-exterior.webp)

What a rendered picture has that the live view has not:

- **Light that bounces.** A room lit by its lamps has its ceiling lit by the light thrown back from the floor and the walls; a corner is darker because less light reaches it, not because of a stand-in shade.
- **Every light.** The live view draws only the nearest lights, a few with shadows; a render takes them all, each with its own soft shadow (a bulb is a few centimetres across, a panel its own size), and a maker's IES distribution shapes its light.
- **The sun's soft shadows and the sky's light** from all of the sky at once.
- **Glass** that bends and reflects the light, and materials as rough or glossy as their maps say (see [Texture sets](materials.md#texture-sets)).

## Render settings

The chip's **Settings…** (or View → **Render settings**) opens the Render dialog:

![The Render dialog](shots/render-dialog.webp)

- **Size**: the window's, **1080p** (1920 wide), **4K** (3840 wide) or your own, up to 8192 pixels a side; 1080p and 4K keep the view's proportion. The saved image is rendered at that size, whatever the window's.
- **Done after**: so many **samples** (64 to 4096: more is cleaner and slower) or so many seconds.
- **Denoise** clears the last grain. In the view Lapin's own denoiser does it, guided by the picture's colours, normals and depth so edges, joints and textures stay sharp. A saved image on the desktop app is denoised by Intel Open Image Denoise, which comes with Lapin; in the browser, by Lapin's own.
- **Depth of field**: the near and the far go soft, as through a camera's lens. **Pick in the view** and click what should be sharp; the **aperture** sets how soft the rest goes (f/1.4 a lot, f/16 hardly).
- **Exposure** and **White balance**: the view's (View → **Display quality**), or the image's own.

**Save image…** renders the image at its size and saves it as a PNG. While it renders, the view shows it coming and the dialog says how far it is (**Stop** gives it up).

The settings are kept on this computer; nothing of them goes into the project.

## By night

Set an hour after sunset in View → **Sun and shadows**: the sky darkens, the exposure opens and the light fixtures light the rooms (see [Lights and the night](views.md#lights-and-the-night)). A render then shows the rooms as their lamps light them, the light bouncing off the walls and the ceiling; at dusk the window still shows the sky's last light.

![A living room at dusk, lit by two downlights, a pendant and a spot](shots/render-interior.webp)

## How fast

A small house renders at 1080p and 256 samples in about a minute on a recent computer; a room at night, where the light bounces more, takes longer. The picture in the view is rendered at the screen's own pixels (on a high-density screen, half its device pixels), so it clears faster; a saved image is rendered at its full size.

The sky is the view's: the project's place, day and hour. A sky photographed (an HDRI) is for a later version, as is rendering an image in tiles larger than the graphics card allows and a queue of views rendered one after another.
