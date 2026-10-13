# Asset-Metadaten

Hier werden die authored Dateien `<assetId>.meta.json` vom späteren Wizard beziehungsweise Seed-Werkzeug abgelegt. Die Kategorievorlagen unter `Assets/templates/` definieren Pflichtfelder; Herkunft, Lizenzstatus, Modell, Seed und Prompt dürfen nicht erfunden werden. Es wurden noch keine Produktionsmetadaten erzeugt.

Die geplanten 37 IDs stehen in `media-source/plan/delivery-list.csv` und sind in `Assets/config/id-register.json` reserviert. Eine Reservierung ist kein Liefer- oder Abnahmenachweis. Zonendateien werden getrennt unter `media-source/zone-authoring/` abgelegt. `manifest.json` und `manifest.md` werden erst durch den Manifest-Build erzeugt; vorhandene Prototypbilder und `artwork.prototype.json` bleiben bis zur geprüften Migration unverändert.