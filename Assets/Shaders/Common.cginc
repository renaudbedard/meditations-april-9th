﻿float3 _BendAxis;
float _BendAngle;
float _Shake;

// https://gist.github.com/keijiro/ee439d5e7388f3aafc5296005c8c3f33
float3x3 AngleAxis3x3(float angle, float3 axis)
{
	float c, s;
	sincos(angle, s, c);

	float t = 1 - c;
	float x = axis.x;
	float y = axis.y;
	float z = axis.z;

	return float3x3(
		t * x * x + c, t * x * y - s * z, t * x * z + s * y,
		t * x * y + s * z, t * y * y + c, t * y * z - s * x,
		t * x * z - s * y, t * y * z + s * x, t * z * z + c
	);
}

float3 Bend(float bendability, float3 vertex)
{
	float variation = _Shake * 0.15f * bendability * abs(_BendAngle);

	float3x3 rotationMatrix = AngleAxis3x3((_BendAngle * bendability + variation) * 0.5f, _BendAxis);
	return mul(rotationMatrix, vertex);
}