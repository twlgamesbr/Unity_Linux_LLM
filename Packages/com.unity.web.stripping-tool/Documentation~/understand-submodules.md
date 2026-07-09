# Understand submodules

Modules and submodules organize code, making it easier to reuse and maintain. Stripping submodules removes unused code, making the Web build smaller and startup faster.

To learn which submodules you can remove from a build, refer to the [Submodule reference](submodule-reference.md).

## Modules

Modules, also known as [built-in packages](xref:pack-build), are discrete components that the Unity engine is composed of. Modules break down complex projects into smaller, more manageable parts, making development and collaboration more efficient. For instance, when you’re developing an application, it contains various systems such as gameplay mechanics, graphics rendering, audio, and more. Each of these systems equates to a module.

## Submodules

Modules are further broken down into submodules. Submodules support specific functionality within a module. Depending on the requirements of the application you're developing, you may not need every submodule a module is composed of. The Web Stripping Tool allows you to remove unnecessary submodules from your application.

## Additional resources

* [Strip submodules from a build](strip-submodules.md)
* [Identify unused submodules](identify-unused-submodules.md)
