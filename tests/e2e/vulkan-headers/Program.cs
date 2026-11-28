[Import("vulkan/vulkan.h", Only = ["vk", "Vk", "VK_"])]
static unsafe partial class Vk { }

static unsafe class Program
{
    static int Main()
    {
        Vk.VkApplicationInfo application = default;
        application.sType = Vk.VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO;
        application.apiVersion = Vk.VK_API_VERSION_1_0;
        Vk.VkInstanceCreateInfo create = default;
        create.sType = Vk.VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO;
        create.pApplicationInfo = &application;
        Vk.VkAllocationCallbacks callbacks = default;
        callbacks.pfnFree = null;
        return sizeof(Vk.VkAllocationCallbacks) == 6 * sizeof(nuint) && sizeof(Vk.VkApplicationInfo) > 0 && sizeof(Vk.VkInstanceCreateInfo) > 0 ? 0 : 1;
    }
}
