'''
This file contains some geometry-related functions I used for collision detection in a Pygame project I
abandoned some years ago. While this isn't my most recent work, it does serve to demonstrate my tendency to
reinvent the wheel rather than grapple with external libraries when I only need a few simple functions to
get things up and running. I'm happy to do either, but sometimes writing things from scratch reduces future
confusion and saves time in the long run.
'''

#!/usr/local/bin/python3
# Name: Simon Katzer
# File: geom.py
# Desc: This file defines a bunch of geometry-related functions.

import entity, wall, math

#########################
# SIMPLE MATH FUNCTIONS #
#########################

def max(a, b):
	return a if a>b else b
def min(a, b):
	return a if a<b else b

def sign(n):
	if n == 0: return 0
	elif n > 0: return 1
	elif n < 0: return -1

def slope(p1, p2): # returns the slope of the line defined by 2 points
	return math.inf if p1[0] == p2[0] else (p2[1]-p1[1])/(p2[0]-p1[0]) # slope = delta y / delta x
def on_segment(point, line):
	# it's colinear
	return (
			(slope(point, line[0]) == slope(point, line[1])) and
			# its x is in the bounding box
			point[0] >= min(line[0][0],line[1][0]) and
			point[0] <= max(line[0][0],line[1][0]) and
			# its y is in the bounding box
			point[1] >= min(line[0][1],line[1][1]) and
			point[1] <= max(line[0][1],line[1][1])
	)
def point_of_intersection(line1, line2):
	#print("Finding intersection of {0} and {1}... ".format(line1, line2),end="")
	#find the equations for the two lines
	slope1 = slope(line1[0], line1[1]) # the slope (m) of line1
	slope2 = slope(line2[0], line2[1]) # the slope (m) of line2
	const1 = 0 if (slope1 == math.inf) else (line1[0][1] - line1[0][0]*slope1) # the constant (b) of line1
	const2 = 0 if (slope2 == math.inf) else (line2[0][1] - line2[0][0]*slope2) # the constant (b) of line2

	if slope1 == slope2: return False # lines are parallel
	
	if math.isinf(slope1):
		intersectionX = line1[0][0]
		intersectionY = slope2 * intersectionX + const2 # mx + b
	elif math.isinf(slope2):
		intersectionX = line2[0][0]
		intersectionY = slope1 * intersectionX + const1 # mx + b
	else:
		slopeTemp = slope1 - slope2;
		constTemp = const2 - const1;
		intersectionX = constTemp / slopeTemp;
		intersectionY = slope1 * intersectionX + const1;
	#print("result: {0}".format((intersectionX, intersectionY)))	
	return (intersectionX, intersectionY)

'''
# https://stackoverflow.com/questions/2259476/rotating-a-point-about-another-point-2d
def rotate_point(cx, cy, angle, p):
	s = sin(angle)
	float c = cos(angle);

	# translate point back to origin:
	p[0] -= cx;
	p[1] -= cy;

	# rotate point
	xnew = p.x * c - p.y * s;
	ynew = p.x * s + p.y * c;

	# translate point back:
	p[0] = xnew + cx;
	p[1] = ynew + cy;
	return p;
'''

###############################
# FUNCTIONS INVOLVING CLASSES #
###############################

def distance(a, b): # use the Pythagorean Theorem to find the distance between two points (tuples) or entities
	if isinstance(a, tuple) and isinstance(b, tuple): return math.sqrt((a[0]-b[0])**2 + (a[1]-b[1])**2)
	elif isinstance(a, tuple) and isinstance(b, entity.Entity): return math.sqrt((a[0]-b.x)**2 + (a[1]-b.y)**2)
	elif isinstance(a, entity.Entity) and isinstance(b, tuple): return distance(b, a)
	elif isinstance(a, entity.Entity) and isinstance(b, entity.Entity): return math.sqrt((a.x-b.x)**2 + (a.y-b.y)**2)

# returns a number indicating which direction this entity should face to look at the specified point. Not very elegant atm
def angle_to_point(entity_, point):
	return -math.degrees(math.atan((point[1]-entity_.y)/(point[0]-entity_.x))) + (180 if (point[0]-entity_.x) < 0 else 0) % 360

def collision(a, b):
	if isinstance(a, entity.Entity) and isinstance(b, entity.Entity):
		return distance(a.center(),b.center()) < a.COLLISION_RADIUS + b.COLLISION_RADIUS
	elif isinstance(a, entity.Entity) and isinstance(b, wall.Wall):
		#rename them so the code is sasier to read
		entity_ = a
		wall_ = b
		for i in range(0,wall_.num_vertices()): # for each side of the wall:
			side = (wall_.vertices[i],wall_.vertices[(i+1)%wall_.num_vertices()]) 				# define the side
			temp_point = () 																	# this point, plus the entity center, will define a line perpendicular to the side of the wall
			if abs(slope(side[0], side[1])) == 0: # if the line is horizontal
				temp_point = (entity_.x, entity_.y + 1)	
			elif math.isinf(slope(side[0],side[1])): temp_point = (entity_.x + 1, entity_.y) 	# if the line is vertical
			else: temp_point = entity_.x, entity_.y + -1/slope(side[0], side[1]) 				# if the line is at some other angle
			intersection = point_of_intersection(side,(entity_.center(),temp_point));
			#if i == 0: print("Point of intersection of side {0},{1} and {2},{3}: {4}".format(side[0], side[1], entity_.center(), temp_point, intersection))
			if (distance(entity_.center(), intersection) < entity_.COLLISION_RADIUS and on_segment(intersection, side)) or distance(entity_.center(), wall_.vertices[i]) < entity_.COLLISION_RADIUS:
				return True
		return False
	elif isinstance(a, wall.Wall) and isinstance(b, entity.Entity): return collision(b, a)

######################
# ENTITY PATHFINDING #
######################

'''
What I need to here is to make a function which determines whether an entity has line of sight to a point (taking into account collision radius
by drawing one line from each side of itself). If one of these raytraces hits a wall, it will attempt new sets of raytraces with offsets of
1 degree ccw, 1 degree cw, 2 degrees ccw, 2 degrees cw, and so on. These new rays will go the same distance as the first one did when it hit
a wall and, if both are successful, it will check for line of sight to the target as if it had just teleported to its new location. If it
still can't see the target, it will attempt to go back to step 1 and find a new location to "teleport" to, and if none of these locations are
successful, it will give up.
'''
