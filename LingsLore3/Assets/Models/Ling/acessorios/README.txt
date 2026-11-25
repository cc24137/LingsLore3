PACOTE DE 7 ADEREÇOS 3D LOW-POLY PARA UNITY
===========================================

Conteúdo do pacote:
1. monoculo          - Monóculo dourado com lente translúcida e corrente pendente
2. tophat            - Cartola preta com aba curvada, fita vermelha e fivela dourada
3. laco_rosa         - Laço rosa estilizado 3D com nó central e pontas em "V"
4. chapeu_helice     - Boné colorido de 6 gomos com aba frontal e hélice separada ("Helice")
5. oculos_quadrado   - Óculos quadrado preto com lentes translúcidas e hastes curvadas
6. chapeu_verde_link - Gorro verde comprido estilo Link (Zelda) com dobra na base
7. chapeu_chines     - Chapéu cônico de palha (dupla face sólida) com borda trançada e anel interno

-------------------------------------------
COMO IMPORTAR NA UNITY
-------------------------------------------
OPÇÃO A — Nativo (Zero plugins): pasta OBJ_MTL_UnityNativo/
  1. Arraste a pasta inteira "OBJ_MTL_UnityNativo" para dentro da janela Project (Assets/) da Unity.
  2. Mantenha os arquivos .obj e .mtl juntos na mesma pasta para a Unity ler todas as cores automaticamente.
  3. Para deixar as lentes do Monóculo e do Óculos Quadrado transparentes:
     - Clique no modelo na Unity -> aba Materials -> Extract Materials.
     - Selecione o material "Vidro_Lente" e mude Surface Type / Rendering Mode para Transparent (Alpha ~0.35).

OPÇÃO B — PBR completo (Blender ou Unity + glTFast): pasta GLB_PBR_Blender_glTFast/
  1. Na Unity, instale o pacote oficial "glTFast" pelo Package Manager (com.unity.cloud.gltfast) e arraste os .glb.
  2. Os arquivos .glb já trazem PBR (Metallic/Roughness), transparência configurada nas lentes e pivô local
     dedicado na malha filha "Helice" do chapéu de hélice.
  3. Também podem ser abertos diretamente no Blender (File > Import > glTF 2.0) caso queira editar ou exportar .fbx.

-------------------------------------------
ESCALA E PIVÔS (Y-Up, Frente em +Z)
-------------------------------------------
- Todos os modelos foram proporcionados para uma cabeça de raio 0.5m (diâmetro 1.0m, igual à Sphere/Capsule padrão da Unity).
- Para personagens humanoides realistas (escala Mixamo, cabeça ~0.22m), basta definir Scale = (0.24, 0.24, 0.24) no Prefab.
- Pivôs (0, 0, 0):
  * tophat, chapeu_helice, chapeu_verde_link, chapeu_chines: centro da abertura inferior (encaixe no topo da cabeça).
  * monoculo: centro da lente (encaixe direto na frente do olho).
  * oculos_quadrado: centro da ponte do nariz.
  * laco_rosa: centro traseiro do nó (encaixe na superfície da cabeça, cabelo ou pescoço).

-------------------------------------------
BÔNUS: GIRAR A HÉLICE
-------------------------------------------
Arraste Scripts_Unity/GirarHelice.cs para o GameObject "chapeu_helice" na Unity e aperte Play!
