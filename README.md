# AreaPlanHelper
A Revit addin to help you create areas from rooms that respect your BOMA or other similar rules.

Developed at the BeyondAEC Hackathon 2019 (in 23 hours!) by Team AreaShooters:
- Chris Mungenast, Elkus Manfredi
- Jess Purcell, Shepley Bulfinch
- Matt Mason, IMAGINiT

The goal was to try to give Revit users who are manually manipulating Areas to meet BOMA or other similar requirements, a head start - maybe 80%, by using Rooms (with a parameter to define Room Types) to create Areas.

The areas are created in the current model, from rooms either in the current model, or in another model linked into the current model (this enables you to create scratch area plans that don't clutter the model and can be re-done or easily wiped when appropriate).

The tool enables you to configure your room type standards, and the "weights" of which  room type the Area Boundary should follow. It also applies:
- Exterior Function walls
- Corridor handling
- and more.

The addin is pre-built in the addin folder (an MSI, which supports Revit 2017, 2018 and 2019)
https://github.com/mattmas/AreaPlanHelper/tree/master/Addin

Beyond the hackathon version, we have a slightly enhanced version that will enable you to pick multiple linked models (such as core and fitout) to pull the rooms from at the same time. We'd love to hear your feedback and suggestions on the Issues page above.

Video Available here:
https://youtu.be/vsXFZWRsbWs
