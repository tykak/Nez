﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;


namespace Nez.Shadows
{
	/// <summary>
	/// Point light that also casts shadows
	/// </summary>
	public class PolygonLight : RenderableComponent
	{
		/// <summary>
		/// layer mask of all the layers this light should interact with. defaults to all layers.
		/// </summary>
		public int collidesWithLayers = Physics.allLayers;

		public override float width { get { return _radius * 2f; } }
		public override float height { get { return _radius * 2f; } }

		public override RectangleF bounds
		{
			get
			{
				if( _areBoundsDirty )
				{
					_bounds.calculateBounds( entity.transform.position, _localOffset, new Vector2( _radius, _radius ), Vector2.One, 0, width, height );
					_areBoundsDirty = false;
				}

				return _bounds;
			}
		}

		/// <summary>
		/// Radius of influence of the light
		/// </summary>
		public float radius
		{
			get { return _radius; }
			set
			{
				if( value != _radius )
				{
					_radius = value;
					_areBoundsDirty = true;

					if( _lightEffect != null )
						_lightEffect.Parameters["lightRadius"].SetValue( radius );
				}
			}
		}

		/// <summary>
		/// Power of the light, from 0 (turned off) to 1 for maximum brightness        
		/// </summary>
		public float power;

		float _radius;
		Effect _lightEffect;
		VisibilityComputer _visibility;
		FastList<short> _indices = new FastList<short>( 50 );
		FastList<VertexPositionTexture> _vertices = new FastList<VertexPositionTexture>( 20 );

		// shared Collider cache used for querying for nearby geometry
		static Collider[] _colliderCache = new Collider[10];


		public PolygonLight( float radius ) : this( radius, Color.White )
		{ }


		public PolygonLight( float radius, Color color ) : this( radius, color, 1.0f )
		{ }


		public PolygonLight( float radius, Color color, float power )
		{
			this.radius = radius;
			this.power = power;
			this.color = color;
			computeTriangleIndices();
		}


		#region Component and RenderableComponent

		public override void onAddedToEntity()
		{
			_lightEffect = entity.scene.content.loadEffect<Effect>( "polygonLight", EffectResource.polygonLightBytes );
			_lightEffect.Parameters["lightRadius"].SetValue( radius );
			_visibility = new VisibilityComputer();
		}


		public override void render( Graphics graphics, Camera camera )
		{
			if( power > 0 && isVisibleFromCamera( camera ) )
			{
				var totalOverlaps = Physics.overlapCircleAll( entity.position + _localOffset, _radius, _colliderCache, collidesWithLayers );

				// compute the visibility mesh
				_visibility.begin( entity.transform.position + _localOffset, _radius );
				for( var i = 0; i < totalOverlaps; i++ )
				{
					if( !_colliderCache[i].isTrigger )
						_visibility.addSquareOccluder( _colliderCache[i].bounds );
				}
				System.Array.Clear( _colliderCache, 0, totalOverlaps );

				// generate a triangle list from the encounter points
				var encounters = _visibility.end();
				generateVertsFromEncounters( encounters );
				ListPool<Vector2>.free( encounters );

				Core.graphicsDevice.BlendState = BlendState.Additive;
				Core.graphicsDevice.RasterizerState = RasterizerState.CullNone;

				// wireframe debug
				//var rasterizerState = new RasterizerState();
				//rasterizerState.FillMode = FillMode.WireFrame;
				//rasterizerState.CullMode = CullMode.None;
				//Core.graphicsDevice.RasterizerState = rasterizerState;

				// Apply the effect
				_lightEffect.Parameters["viewProjectionMatrix"].SetValue( entity.scene.camera.viewProjectionMatrix );
				_lightEffect.Parameters["lightSource"].SetValue( entity.transform.position );
				_lightEffect.Parameters["lightColor"].SetValue( color.ToVector3() * power );
				_lightEffect.Techniques[0].Passes[0].Apply();

				var primitiveCount = _vertices.length / 2;
				Core.graphicsDevice.DrawUserIndexedPrimitives( PrimitiveType.TriangleList, _vertices.buffer, 0, _vertices.length, _indices.buffer, 0, primitiveCount );
			}
		}

		#endregion


		/// <summary>
		/// adds a vert to the list
		/// </summary>
		/// <param name="position">Position.</param>
		/// <param name="texCoord">Tex coordinate.</param>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		void addVert( Vector3 position, Vector2 texCoord )
		{
			var index = _vertices.length;
			_vertices.ensureCapacity();
			_vertices.buffer[index].Position = position;
			_vertices.buffer[index].TextureCoordinate = texCoord;
			_vertices.length++;
		}


		void computeTriangleIndices( int totalTris = 20 )
		{
			_indices.reset();

			// compute the indices to form triangles
			for( var i = 0; i < totalTris; i += 2 )
			{
				_indices.add( 0 );
				_indices.add( (short)( i + 2 ) );
				_indices.add( (short)( i + 1 ) );
			}
		}


		void generateVertsFromEncounters( List<Vector2> encounters )
		{
			_vertices.reset();

			// add a vertex for the center of the mesh
			addVert( new Vector3( entity.transform.position.X, entity.transform.position.Y, 0 ), entity.transform.position );

			// add all the other encounter points as vertices storing their world position as UV coordinates
			for( var i = 0; i < encounters.Count; i++ )
				addVert( encounters[i].toVector3(), encounters[i] );

			// if we dont have enough tri indices add enough for our encounter list
			var triIndices = _indices.length / 3;
			if( encounters.Count > triIndices )
				computeTriangleIndices( encounters.Count );
		}

	}
}
