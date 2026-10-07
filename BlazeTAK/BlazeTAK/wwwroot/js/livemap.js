window.liveMap = {
    instances: {},
    markers: {},
    dotNetRefs: {},
    placementModes: {},

    init: function (elementId, initialLat, initialLng, initialZoom, dotNetRef) {
        if (typeof L === 'undefined') {
            console.error('Leaflet is not loaded.');
            return;
        }

        // Clean up previous instance on same container if exists
        if (this.instances[elementId]) {
            this.instances[elementId].remove();
            delete this.instances[elementId];
            delete this.markers[elementId];
            delete this.dotNetRefs[elementId];
        }

        var mapElement = document.getElementById(elementId);
        if (!mapElement) {
            return;
        }

        var lat = initialLat !== undefined && initialLat !== null ? initialLat : 47.5;
        var lng = initialLng !== undefined && initialLng !== null ? initialLng : 14.5;
        var zoom = initialZoom !== undefined && initialZoom !== null ? initialZoom : 6;

        var map = L.map(elementId).setView([lat, lng], zoom);

        L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
            maxZoom: 17,
            attribution: 'Map data: &copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors, <a href="http://viewfinderpanoramas.org">SRTM</a> | Map style: &copy; <a href="https://opentopomap.org">OpenTopoMap</a> (<a href="https://creativecommons.org/licenses/by-sa/3.0/">CC-BY-SA</a>)'
        }).addTo(map);

        this.instances[elementId] = map;
        this.markers[elementId] = {};
        this.dotNetRefs[elementId] = dotNetRef;
        this.placementModes[elementId] = false;

        var self = this;
        map.on('click', function (e) {
            if (self.placementModes[elementId] && self.dotNetRefs[elementId]) {
                self.dotNetRefs[elementId].invokeMethodAsync('OnMapClicked', e.latlng.lat, e.latlng.lng);
            }
        });

        // Invalidate size to ensure full rendering if container size changed
        setTimeout(function () {
            map.invalidateSize();
        }, 200);
    },

    setPlacementMode: function (elementId, isPlacing) {
        this.placementModes[elementId] = !!isPlacing;
        var mapElement = document.getElementById(elementId);
        if (mapElement) {
            if (isPlacing) {
                mapElement.style.cursor = 'crosshair';
            } else {
                mapElement.style.cursor = '';
            }
        }
    },

    createMilsymbolIcon: function (sidc, name, size) {
        var iconSize = size || 30;
        if (typeof ms !== 'undefined' && ms.Symbol) {
            try {
                var sym = new ms.Symbol(sidc || 'SFGPUCI----', {
                    size: iconSize,
                    uniqueDesignation: name || ''
                });
                var anchor = sym.getAnchor();
                var symSize = sym.getSize();
                return L.icon({
                    iconUrl: sym.toDataURL(),
                    iconSize: [symSize.width, symSize.height],
                    iconAnchor: [anchor.x, anchor.y],
                    popupAnchor: [0, -anchor.y]
                });
            } catch (err) {
                console.warn('milsymbol error generating icon for ' + sidc, err);
            }
        }

        // Fallback default Leaflet icon
        return new L.Icon.Default();
    },

    getSymbolSvgString: function (sidc, name, size) {
        if (typeof ms !== 'undefined' && ms.Symbol) {
            try {
                var sym = new ms.Symbol(sidc || 'SFGPUCI----', {
                    size: size || 24,
                    uniqueDesignation: name || ''
                });
                return sym.asSVG();
            } catch (err) {
                console.warn('milsymbol asSVG error:', err);
            }
        }
        return '';
    },

    getSymbolDataUrl: function (sidc, name, size) {
        if (typeof ms !== 'undefined' && ms.Symbol) {
            try {
                var sym = new ms.Symbol(sidc || 'SFGPUCI----', {
                    size: size || 24,
                    uniqueDesignation: name || ''
                });
                return sym.toDataURL();
            } catch (err) {
                console.warn('milsymbol toDataURL error:', err);
            }
        }
        return '';
    },

    addOrUpdateMarker: function (elementId, markerDto) {
        var map = this.instances[elementId];
        if (!map || !markerDto) return;

        var markers = this.markers[elementId] || (this.markers[elementId] = {});
        var existingMarker = markers[markerDto.id];

        var icon = this.createMilsymbolIcon(markerDto.unitType, markerDto.name, 30);
        var latLng = [markerDto.latitude, markerDto.longitude];

        var popupContent = `
            <div style="min-width: 200px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; color: var(--text);">
                <div style="font-size: 15px; font-weight: bold; border-bottom: 2px solid var(--front); padding-bottom: 4px; margin-bottom: 8px; color: var(--front);">
                    ${this.escapeHtml(markerDto.name || 'Tactical Marker')}
                </div>
                <div style="font-size: 12px; margin-bottom: 4px;"><strong>Type (SIDC):</strong> <span class="badge badge-theme" style="font-family: monospace;">${this.escapeHtml(markerDto.unitType || 'N/A')}</span></div>
                ${markerDto.description ? `<div style="font-size: 12px; margin-bottom: 6px; opacity: 0.9;"><strong>Description:</strong> ${this.escapeHtml(markerDto.description)}</div>` : ''}
                <div style="font-size: 11px; opacity: 0.75; margin-top: 6px;">
                    <div><strong>Lat:</strong> ${markerDto.latitude.toFixed(5)}, <strong>Lng:</strong> ${markerDto.longitude.toFixed(5)}</div>
                    <div><strong>Created:</strong> ${new Date(markerDto.timeStamp_Created).toLocaleString()}</div>
                    ${markerDto.createdByName ? `<div><strong>By:</strong> ${this.escapeHtml(markerDto.createdByName)}</div>` : ''}
                </div>
                <div style="margin-top: 8px; padding-top: 6px; border-top: 1px solid var(--front); font-size: 11px; opacity: 0.75;">
                    <em>Right-click marker for actions (Move, Edit, Delete)</em>
                </div>
            </div>
        `;

        var self = this;

        if (existingMarker) {
            existingMarker.setLatLng(latLng);
            existingMarker.setIcon(icon);
            existingMarker.setPopupContent(popupContent);
            existingMarker.markerData = markerDto;
        } else {
            var marker = L.marker(latLng, {
                icon: icon,
                draggable: false
            }).addTo(map);

            marker.markerData = markerDto;
            marker.bindPopup(popupContent);
            marker.bindTooltip(markerDto.name || 'Tactical Marker', { permanent: false, direction: 'top', offset: [0, -15] });

            // Right-click contextmenu event on marker
            marker.on('contextmenu', function (e) {
                if (L.DomEvent) {
                    L.DomEvent.stopPropagation(e);
                }
                var mapContainer = document.getElementById(elementId);
                var containerRect = mapContainer ? mapContainer.getBoundingClientRect() : { left: 0, top: 0 };
                var clientX = e.originalEvent.clientX;
                var clientY = e.originalEvent.clientY;
                var relX = clientX - containerRect.left;
                var relY = clientY - containerRect.top;

                if (self.dotNetRefs[elementId]) {
                    self.dotNetRefs[elementId].invokeMethodAsync(
                        'OnMarkerContextMenu',
                        markerDto.id,
                        relX,
                        relY,
                        clientX,
                        clientY
                    );
                }
            });

            // Drag end event
            marker.on('dragend', function (e) {
                var newPos = e.target.getLatLng();
                if (self.dotNetRefs[elementId]) {
                    self.dotNetRefs[elementId].invokeMethodAsync(
                        'OnMarkerDragEnd',
                        markerDto.id,
                        newPos.lat,
                        newPos.lng
                    );
                }
            });

            markers[markerDto.id] = marker;
        }
    },

    setMarkerDraggable: function (elementId, markerId, isDraggable) {
        var markers = this.markers[elementId];
        if (markers && markers[markerId]) {
            var marker = markers[markerId];
            if (marker.dragging) {
                if (isDraggable) {
                    marker.dragging.enable();
                } else {
                    marker.dragging.disable();
                }
            }
        }
    },

    setAllMarkersDraggable: function (elementId, isDraggable) {
        var markers = this.markers[elementId];
        if (markers) {
            for (var id in markers) {
                if (markers.hasOwnProperty(id) && markers[id].dragging) {
                    if (isDraggable) {
                        markers[id].dragging.enable();
                    } else {
                        markers[id].dragging.disable();
                    }
                }
            }
        }
    },

    loadMarkers: function (elementId, markersList) {
        if (!markersList || !Array.isArray(markersList)) return;
        for (var i = 0; i < markersList.length; i++) {
            this.addOrUpdateMarker(elementId, markersList[i]);
        }
    },

    removeMarker: function (elementId, markerId) {
        var markers = this.markers[elementId];
        if (markers && markers[markerId]) {
            var map = this.instances[elementId];
            if (map) {
                map.removeLayer(markers[markerId]);
            }
            delete markers[markerId];
        }
    },

    destroy: function (elementId) {
        if (this.instances[elementId]) {
            this.instances[elementId].remove();
            delete this.instances[elementId];
            delete this.markers[elementId];
            delete this.dotNetRefs[elementId];
            delete this.placementModes[elementId];
        }
    },

    escapeHtml: function (str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }
};
