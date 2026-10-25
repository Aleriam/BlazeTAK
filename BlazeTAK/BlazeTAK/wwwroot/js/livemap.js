window.liveMap = {
    instances: {},
    markers: {},
    userLocationMarkers: {},
    unitLiveMarkers: {},
    dotNetRefs: {},
    placementModes: {},
    watchIds: {},
    lastSentPositions: {},

    init: function (elementId, initialLat, initialLng, initialZoom, dotNetRef) {
        if (typeof L === 'undefined') {
            console.error('Leaflet is not loaded.');
            return;
        }

        // Clean up previous instance on same container if exists
        if (this.instances[elementId]) {
            this.destroy(elementId);
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
        this.userLocationMarkers[elementId] = {};
        this.unitLiveMarkers[elementId] = {};
        this.dotNetRefs[elementId] = dotNetRef;
        this.placementModes[elementId] = false;

        var self = this;
        map.on('click', function (e) {
            if (self.placementModes[elementId] && self.dotNetRefs[elementId]) {
                self.dotNetRefs[elementId].invokeMethodAsync('OnMapClicked', e.latlng.lat, e.latlng.lng);
            }
            else
            {
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

    createMilsymbolIcon: function (sidc, name, size, badgeCount) {
        var iconSize = size || 30;
        if (typeof ms !== 'undefined' && ms.Symbol) {
            try {
                var sym = new ms.Symbol(sidc || 'SFGPUCI----', {
                    size: iconSize,
                    uniqueDesignation: name || ''
                });
                var anchor = sym.getAnchor();
                var symSize = sym.getSize();

                if (badgeCount && badgeCount > 1) {
                    var html = `
                        <div style="position: relative; width: ${symSize.width}px; height: ${symSize.height}px;">
                            <img src="${sym.toDataURL()}" style="width: 100%; height: 100%; display: block;" />
                            <span class="badge bg-danger rounded-pill" style="position: absolute; top: -6px; right: -6px; font-size: 11px; padding: 2px 6px; box-shadow: 0 1px 3px rgba(0,0,0,0.5);">${badgeCount}</span>
                        </div>
                    `;
                    return L.divIcon({
                        html: html,
                        className: 'custom-milsymbol-badge-icon',
                        iconSize: [symSize.width, symSize.height],
                        iconAnchor: [anchor.x, anchor.y],
                        popupAnchor: [0, -anchor.y]
                    });
                }

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

    createUserLocationIcon: function (isSelf, heading, callSign, name) {
        var baseColor = isSelf ? '#00E676' : '#00B0FF';
        var angle = (typeof heading === 'number' && !isNaN(heading)) ? heading : 0;
        var hasHeading = typeof heading === 'number' && !isNaN(heading);
        var labelText = callSign || name || (isSelf ? 'You' : 'Member');

        var arrowSvg = hasHeading ? `
            <g transform="rotate(${angle} 20 20)">
                <path d="M 20 5 L 27 24 L 20 19 L 13 24 Z" fill="#FFFFFF" stroke="#111111" stroke-width="1.5" stroke-linejoin="round" />
            </g>
        ` : `
            <circle cx="20" cy="20" r="5" fill="#FFFFFF" stroke="#111111" stroke-width="1.5" />
        `;

        var html = `
            <div class="user-position-marker ${isSelf ? 'user-self' : 'user-other'}" style="position: relative; width: 40px; height: 40px; cursor: pointer;">
                <svg width="40" height="40" viewBox="0 0 40 40" style="filter: drop-shadow(0 2px 5px rgba(0,0,0,0.6));">
                    <circle cx="20" cy="20" r="14" fill="${baseColor}" stroke="#FFFFFF" stroke-width="2.5" fill-opacity="0.95" />
                    ${arrowSvg}
                </svg>
                <div style="position: absolute; top: 38px; left: 50%; transform: translateX(-50%); white-space: nowrap; font-size: 11px; font-weight: 600; background: rgba(20,20,20,0.85); color: #fff; padding: 1px 6px; border-radius: 4px; border: 1px solid rgba(255,255,255,0.2); pointer-events: none; text-shadow: 0 1px 2px #000;">
                    ${this.escapeHtml(labelText)}
                </div>
            </div>
        `;

        return L.divIcon({
            html: html,
            className: 'custom-user-location-icon',
            iconSize: [40, 40],
            iconAnchor: [20, 20],
            popupAnchor: [0, -20]
        });
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

    addOrUpdateUserLocation: function (elementId, locDto, isSelf) {
        var map = this.instances[elementId];
        if (!map || !locDto) return;

        var userMarkers = this.userLocationMarkers[elementId] || (this.userLocationMarkers[elementId] = {});
        var existing = userMarkers[locDto.userId];

        var icon = this.createUserLocationIcon(isSelf, locDto.heading, locDto.callSign, locDto.name);
        var latLng = [locDto.latitude, locDto.longitude];

        var roleBadge = isSelf ? '<span class="badge bg-success ms-1">You</span>' : '<span class="badge bg-primary ms-1">Unit Member</span>';
        var leaderBadge = locDto.isLeader ? '<span class="badge bg-warning text-dark ms-1">Leader</span>' : '';

        var popupContent = `
            <div style="min-width: 180px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;">
                <div style="font-size: 14px; font-weight: bold; border-bottom: 2px solid ${isSelf ? '#00E676' : '#00B0FF'}; padding-bottom: 3px; margin-bottom: 6px;">
                    ${this.escapeHtml(locDto.name || 'User')} ${roleBadge} ${leaderBadge}
                </div>
                <div style="font-size: 12px; margin-bottom: 3px;"><strong>CallSign:</strong> <span class="badge bg-secondary font-monospace">${this.escapeHtml(locDto.callSign || 'N/A')}-${locDto.callSignNumber || 0}</span></div>
                <div style="font-size: 11px; opacity: 0.85; margin-top: 4px;">
                    <div><strong>Lat:</strong> ${locDto.latitude.toFixed(5)}, <strong>Lng:</strong> ${locDto.longitude.toFixed(5)}</div>
                    ${locDto.heading !== undefined && locDto.heading !== null ? `<div><strong>Heading:</strong> ${Math.round(locDto.heading)}&deg;</div>` : ''}
                    ${locDto.speed !== undefined && locDto.speed !== null ? `<div><strong>Speed:</strong> ${(locDto.speed * 3.6).toFixed(1)} km/h</div>` : ''}
                    ${locDto.accuracy !== undefined && locDto.accuracy !== null ? `<div><strong>Accuracy:</strong> &plusmn;${Math.round(locDto.accuracy)}m</div>` : ''}
                    <div><strong>Updated:</strong> ${new Date(locDto.timestamp).toLocaleTimeString()}</div>
                </div>
            </div>
        `;

        if (existing) {
            existing.setLatLng(latLng);
            existing.setIcon(icon);
            existing.setPopupContent(popupContent);
            existing.userLocationData = locDto;
        } else {
            var marker = L.marker(latLng, {
                icon: icon,
                zIndexOffset: isSelf ? 1000 : 800
            }).addTo(map);

            marker.userLocationData = locDto;
            marker.bindPopup(popupContent);
            marker.bindTooltip((locDto.callSign || locDto.name || (isSelf ? 'You' : 'Member')), { permanent: false, direction: 'top', offset: [0, -20] });
            userMarkers[locDto.userId] = marker;
        }
    },

    removeUserLocation: function (elementId, userId) {
        var userMarkers = this.userLocationMarkers[elementId];
        if (userMarkers && userMarkers[userId]) {
            var map = this.instances[elementId];
            if (map) {
                map.removeLayer(userMarkers[userId]);
            }
            delete userMarkers[userId];
        }
    },

    loadUserLocations: function (elementId, locationsList, currentUserId) {
        if (!locationsList || !Array.isArray(locationsList)) return;
        for (var i = 0; i < locationsList.length; i++) {
            var loc = locationsList[i];
            var isSelf = (currentUserId && loc.userId.toLowerCase() === currentUserId.toLowerCase());
            this.addOrUpdateUserLocation(elementId, loc, isSelf);
        }
    },

    addOrUpdateLiveUnitMarker: function (elementId, unitMarkerDto) {
        var map = this.instances[elementId];
        if (!map || !unitMarkerDto) return;

        var unitMarkers = this.unitLiveMarkers[elementId] || (this.unitLiveMarkers[elementId] = {});
        var existing = unitMarkers[unitMarkerDto.id];

        var icon = this.createMilsymbolIcon(unitMarkerDto.unitType, unitMarkerDto.unitName, 34, unitMarkerDto.memberCount);
        var latLng = [unitMarkerDto.latitude, unitMarkerDto.longitude];

        var membersHtml = '';
        if (unitMarkerDto.memberNames && unitMarkerDto.memberNames.length > 0) {
            membersHtml = `<div style="font-size: 11px; margin-top: 4px;"><strong>Members in range (${unitMarkerDto.memberCount}):</strong><div style="opacity: 0.85;">${this.escapeHtml(unitMarkerDto.memberNames.join(', '))}</div></div>`;
        }

        var popupContent = `
            <div style="min-width: 200px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;">
                <div style="font-size: 14px; font-weight: bold; border-bottom: 2px solid #0d6efd; padding-bottom: 3px; margin-bottom: 6px;">
                    <i class="bi bi-shield-shaded me-1"></i>${this.escapeHtml(unitMarkerDto.unitName || 'Unit')}
                    ${unitMarkerDto.isLeaderMarker ? '<span class="badge bg-warning text-dark ms-1">HQ / Leader</span>' : '<span class="badge bg-info text-dark ms-1">Squad Group</span>'}
                </div>
                <div style="font-size: 12px; margin-bottom: 3px;"><strong>CallSign:</strong> <span class="badge bg-secondary font-monospace">${this.escapeHtml(unitMarkerDto.callSign || 'N/A')}-${unitMarkerDto.callSignNumber || 0}</span></div>
                <div style="font-size: 12px; margin-bottom: 3px;"><strong>Type (SIDC):</strong> <span class="badge bg-dark font-monospace">${this.escapeHtml(unitMarkerDto.unitType || 'SFGPUCI----')}</span></div>
                <div style="font-size: 12px; margin-bottom: 3px;"><strong>Marker Radius:</strong> ${unitMarkerDto.markerRadius || 50}m</div>
                ${membersHtml}
                <div style="font-size: 11px; opacity: 0.75; margin-top: 6px;">
                    <div><strong>Position:</strong> ${unitMarkerDto.latitude.toFixed(5)}, ${unitMarkerDto.longitude.toFixed(5)}</div>
                    <div><strong>Updated:</strong> ${new Date(unitMarkerDto.timestamp).toLocaleTimeString()}</div>
                </div>
            </div>
        `;

        if (existing) {
            existing.marker.setLatLng(latLng);
            existing.marker.setIcon(icon);
            existing.marker.setPopupContent(popupContent);
            existing.unitMarkerData = unitMarkerDto;

            if (existing.circle) {
                existing.circle.setLatLng(latLng);
                existing.circle.setRadius(unitMarkerDto.markerRadius || 50);
            }
        } else {
            var marker = L.marker(latLng, {
                icon: icon,
                zIndexOffset: 500
            }).addTo(map);

            marker.unitMarkerData = unitMarkerDto;
            marker.bindPopup(popupContent);
            marker.bindTooltip(`${unitMarkerDto.unitName} (${unitMarkerDto.memberCount})`, { permanent: false, direction: 'top', offset: [0, -18] });

            var circle = null;
            if (unitMarkerDto.markerRadius && unitMarkerDto.markerRadius > 0) {
                circle = L.circle(latLng, {
                    radius: unitMarkerDto.markerRadius,
                    color: '#0d6efd',
                    fillColor: '#0d6efd',
                    fillOpacity: 0.08,
                    weight: 1.5,
                    dashArray: '5, 5'
                }).addTo(map);
            }

            unitMarkers[unitMarkerDto.id] = {
                marker: marker,
                circle: circle,
                unitMarkerData: unitMarkerDto
            };
        }
    },

    loadLiveUnitMarkers: function (elementId, unitMarkersList) {
        if (!unitMarkersList || !Array.isArray(unitMarkersList)) return;
        var unitMarkers = this.unitLiveMarkers[elementId] || (this.unitLiveMarkers[elementId] = {});
        var newIds = new Set(unitMarkersList.map(function (m) { return m.id; }));

        // Remove old markers not in new list
        var map = this.instances[elementId];
        for (var id in unitMarkers) {
            if (unitMarkers.hasOwnProperty(id) && !newIds.has(id)) {
                if (map) {
                    map.removeLayer(unitMarkers[id].marker);
                    if (unitMarkers[id].circle) {
                        map.removeLayer(unitMarkers[id].circle);
                    }
                }
                delete unitMarkers[id];
            }
        }

        for (var i = 0; i < unitMarkersList.length; i++) {
            this.addOrUpdateLiveUnitMarker(elementId, unitMarkersList[i]);
        }
    },

    startGeolocationTracking: function (elementId, dotNetRef) {
        if (!navigator.geolocation) {
            console.warn('Geolocation is not supported by this browser.');
            return false;
        }

        var self = this;
        if (this.watchIds[elementId]) {
            navigator.geolocation.clearWatch(this.watchIds[elementId]);
            delete this.watchIds[elementId];
        }

        this.watchIds[elementId] = navigator.geolocation.watchPosition(
            function (position) {
                var lat = position.coords.latitude;
                var lng = position.coords.longitude;
                var heading = position.coords.heading;
                var speed = position.coords.speed;
                var accuracy = position.coords.accuracy;

                var lastPos = self.lastSentPositions[elementId];
                var shouldSend = false;

                if (!lastPos) {
                    shouldSend = true;
                } else {
                    var dist = self.calculateDistance(lastPos.lat, lastPos.lng, lat, lng);
                    if (dist >= 10.0) { // Updated when user moves by more than 10 meters
                        shouldSend = true;
                    }
                }

                if (shouldSend) {
                    self.lastSentPositions[elementId] = { lat: lat, lng: lng };
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnLocationChanged', lat, lng, heading, speed, accuracy);
                    }
                }
            },
            function (error) {
                console.warn('Geolocation watch error:', error.message);
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnGeolocationError', error.message);
                }
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 3000
            }
        );

        return true;
    },

    stopGeolocationTracking: function (elementId) {
        if (this.watchIds[elementId]) {
            navigator.geolocation.clearWatch(this.watchIds[elementId]);
            delete this.watchIds[elementId];
            delete this.lastSentPositions[elementId];
        }
    },

    centerMap: function (elementId, lat, lng, zoom) {
        var map = this.instances[elementId];
        if (map && lat !== undefined && lng !== undefined) {
            if (zoom !== undefined && zoom !== null) {
                map.setView([lat, lng], zoom);
            } else {
                map.panTo([lat, lng]);
            }
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
        this.stopGeolocationTracking(elementId);

        var map = this.instances[elementId];
        if (map) {
            map.remove();
        }
        delete this.instances[elementId];
        delete this.markers[elementId];
        delete this.userLocationMarkers[elementId];
        delete this.unitLiveMarkers[elementId];
        delete this.dotNetRefs[elementId];
        delete this.placementModes[elementId];
        delete this.lastSentPositions[elementId];
    },

    calculateDistance: function (lat1, lon1, lat2, lon2) {
        var R = 6371000; // meters
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
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
