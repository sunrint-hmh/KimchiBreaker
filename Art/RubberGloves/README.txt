KimchiBreaker / Rubber gloves

Style matched to the approved mat and dirty-glass jar:
- Rounded low-poly hand silhouette (about 5.6k triangles per hand, including sleeve).
- Current Unity atlas: Glove_PSX_Source.png, imported at 128 x 128 with Point sampling.
- Familiar pink household rubber: smooth dorsal surface, shallow palm grip and cuff edge ribs.
- PS1-inspired 32-color material with ordered dithering; no large wrapping folds.
- Albedo is sRGB. Current Unity material uses the PSXRubber shader, not the old PBR maps.
- Original 256px normal, roughness and metallic/smoothness files remain as source assets.

Sources: RubberGloves.blend and build_rubber_gloves.py.
The Blender source retains its original material; the revised Unity appearance is
defined by Glove_PSX_Source.png and Assets/Shaders/PSXRubber.shader.
See PSX_TEXTURE_WORKFLOW.txt and LATEX_REFERENCE_PROMPT.txt for the current texture.
Unity models/material: Assets/3d_models/RubberGloves/.
Apply Assets/Prefabs/LocalRubberGloveHands.prefab as a child of InGame Main Camera.
Only one pair belongs to the local camera. The same asset can be used by every player.

ClickPerformed fires once per click, even when no ingredient is hit.
LocalRubberGloveHands.SwingStarted provides hand index, world target and click damage.
Neither the visual prefab nor its animation applies damage or changes the score.
Remote avatar attachment and synchronization remain Yechan's responsibility.

Stretch behavior:
- The forearm base remains pinned, while a private copy of the glove mesh stretches.
- The leading fingertip reaches the actual click-ray surface, without the old short visual cap.
- A moving collider is tracked in its own local space during extension and contact.
- Empty-space clicks use the existing two-meter visual target; ingredient attack reach is unchanged.
- UVs are retained, so the rubber texture visibly stretches with the mesh.
- Both FBX models need Read/Write enabled. Shared imported meshes are never modified.
- Runtime mesh copies are restored/disposed when the hands are disabled/destroyed.

Natural-hand revision:
- Continuous molded palm, finger webs and thumb base, without the old box-like palm ridge.
- Unequal finger lengths, slightly splayed open fingers, rounded fingertips and a relaxed thumb.
- Upright first-person idle pose at the left/right sides of the screen.
- Existing Unity texture files, material settings and stretch behavior are preserved.

Anatomical-facing correction:
- Real hand-photo references are recorded under References/README.txt.
- Left/Right meshes now match the actual first-person hands.
- The viewer sees the hand backs; palmar surfaces face outward toward the scene.
- Removed the erroneous extra 180-degree model roll. Upright idle layout is retained.
