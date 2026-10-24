#!/usr/bin/env python3
"""Export cycling directions as Unity-friendly JSON using HeiGIT openrouteservice."""
import argparse
import json
import os
import sys
import math
from pathlib import Path

import requests

API_URL = 'https://api.heigit.org/openrouteservice/v2/directions/cycling-regular/json'
PRESETS = {
    'tower': {'name': 'Calgary Tower', 'lat': 51.0443, 'lon': -114.0631},
    'island': {'name': "Prince's Island Park", 'lat': 51.0558, 'lon': -114.0703},
}


def decode_polyline(encoded, precision=5):
    """Decode Google's encoded polyline into ordered [latitude, longitude] pairs."""
    if not isinstance(encoded, str) or not encoded:
        raise ValueError("Empty or invalid encoded polyline")
    coordinates = []
    index = lat = lon = 0
    factor = 10 ** precision
    while index < len(encoded):
        diffs = []
        for _ in range(2):
            shift = result = 0
            while True:
                if index >= len(encoded):
                    raise ValueError('Invalid encoded polyline from API')
                byte = ord(encoded[index]) - 63
                index += 1
                if not 0 <= byte <= 63 or shift > 30:
                    raise ValueError("Invalid encoded polyline")
                result |= (byte & 0x1F) << shift
                shift += 5
                if byte < 0x20:
                    break
            diffs.append(~(result >> 1) if result & 1 else result >> 1)
        lat += diffs[0]
        lon += diffs[1]
        coordinates.append([round(lat / factor, 7), round(lon / factor, 7)])
    return coordinates


def request_route(key, start, end):
    validate_point(start)
    validate_point(end)
    payload = {
        'coordinates': [[start['lon'], start['lat']], [end['lon'], end['lat']]],
        'instructions': True,
        'elevation': False,
        'units': 'm',
        'language': 'en',
    }
    try:
        response = requests.post(
            API_URL,
            headers={'Authorization': key, 'Content-Type': 'application/json', 'Accept': 'application/json'},
            json=payload, timeout=40,
        )
    except requests.RequestException:
        raise RuntimeError('Routing request failed (network error or timeout)') from None
    if not 200 <= response.status_code < 300:
        raise RuntimeError(f'Routing API returned HTTP {response.status_code}; check credentials, quota, and route accessibility')
    try:
        body = response.json()
    except ValueError:
        raise RuntimeError('Routing API returned invalid JSON') from None
    if not isinstance(body, dict):
        raise RuntimeError('Routing API returned an invalid response')
    routes = body.get('routes') or []
    if not routes:
        raise RuntimeError('No routes returned; choose cycle-accessible points')
    route = routes[0]
    polyline = route.get('geometry', '')
    if not polyline:
        raise RuntimeError('No encoded route geometry returned')
    coords = decode_polyline(polyline)
    if len(coords) < 2:
        raise ValueError('Route must contain at least two points')
    for lat, lon in coords:
        validate_point({'lat': lat, 'lon': lon})
    steps = []
    for segment in route.get('segments', []):
        for step in segment.get('steps', []):
            steps.append({
                'instruction': step.get('instruction', ''),
                'name': step.get('name', ''),
                'distanceMeters': step.get('distance'),
                'durationSeconds': step.get('duration'),
                'maneuverType': step.get('type'),
                'wayPoints': step.get('way_points', []),
            })
    summary = route.get('summary', {})
    for field in ('distance', 'duration'):
        value = summary.get(field)
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
            raise ValueError('Route summary is missing a valid distance or duration')
    return {
        'source': 'HeiGIT openrouteservice / OpenStreetMap',
        'travelMode': 'BICYCLE',
        'origin': endpoint(start),
        'destination': endpoint(end),
        'distanceMeters': summary.get('distance'),
        'durationSeconds': summary.get('duration'),
        'encodedPolyline': polyline,
        'coordinatesLatLng': coords,
        'coordinatesLonLat': [[lon, lat] for lat, lon in coords],
        'steps': steps,
    }


def validate_point(point):
    for field, limit in (('lon', 180), ('lat', 90)):
        value = point[field]
        if not math.isfinite(value) or not -limit <= value <= limit:
            raise ValueError(f'{field} must be finite and between {-limit} and {limit}')


def endpoint(point):
    result = {'longitude': point['lon'], 'latitude': point['lat']}
    if 'name' in point:
        result['name'] = point['name']
    return result


def parse_args(argv=None):
    parser = argparse.ArgumentParser(description='Export a cycling route as Unity-friendly JSON')
    parser.add_argument('--start', choices=sorted(PRESETS), default='tower')
    parser.add_argument('--end', choices=sorted(PRESETS), default='island')
    for flag in ('start-lon', 'start-lat', 'end-lon', 'end-lat'):
        parser.add_argument('--' + flag, type=float)
    parser.add_argument('--output', default='route.json')
    args = parser.parse_args(argv)
    for prefix, preset in (('start', args.start), ('end', args.end)):
        lon, lat = getattr(args, prefix + '_lon'), getattr(args, prefix + '_lat')
        if (lon is None) != (lat is None):
            parser.error(f'--{prefix}-lon and --{prefix}-lat must be supplied together')
        point = dict(PRESETS[preset]) if lon is None else {'lon': lon, 'lat': lat}
        try:
            validate_point(point)
        except ValueError as exc:
            parser.error(str(exc))
        setattr(args, prefix + '_point', point)
    return args


def main(argv=None):
    args = parse_args(argv)
    key = os.environ.get('HEIGIT_API_KEY', '').strip()
    if not key:
        print('Missing API key: set HEIGIT_API_KEY in your terminal.', file=sys.stderr)
        return 1
    try:
        data = request_route(key, args.start_point, args.end_point)
        output = Path(args.output)
        output.write_text(json.dumps(data, indent=2, ensure_ascii=False, allow_nan=False) + '\n', encoding='utf-8')
        print(f'Wrote {output.resolve()}')
        print(f"Distance: {data['distanceMeters']} m | Steps: {len(data['steps'])} | Route points: {len(data['coordinatesLonLat'])}")
    except OSError:
        print('Could not write output file; check the directory and permissions.', file=sys.stderr)
        return 1
    except (ValueError, KeyError, TypeError, IndexError, AttributeError, RuntimeError):
        # Never echo raw response data or exception text that might contain credentials.
        print('Could not export route: network/API failure or invalid route response. Check connectivity, credentials, quota and cycle-accessible coordinates.', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
